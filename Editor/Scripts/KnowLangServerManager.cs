using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityKnowLang.Editor
{
    
    #region Platform Helper
    /// <summary>
    /// Provides platform-specific utilities and path resolution
    /// </summary>
    public class KnowLangPlatformHelper
    {
        const string kPackageName = "dev.knowlang.knowlang";

        public string GetPlatformName()
        {
#if UNITY_EDITOR_WIN
            return "windows";
#elif UNITY_EDITOR_OSX
            return "macos";
#elif UNITY_EDITOR_LINUX
            return "linux";
#else
            return "unknown";
#endif
        }

        public string GetExecutableName()
        {
#if UNITY_EDITOR_WIN
            return "main.exe";
#elif UNITY_EDITOR_OSX
            return "main";
#elif UNITY_EDITOR_LINUX
            return "main";
#else
            return "main";
#endif
        }

        public string PlatformArchiveFile => $"knowlang-unity-{GetPlatformName()}-latest.tar.gz";

        public string GetPackageRoot()
        {
            // Try to find the package root by looking for package.json
            string currentDir = Path.GetDirectoryName(Application.dataPath);

            // For UPM packages
            string[] upmPaths = {
                Path.Combine(currentDir, "Packages", kPackageName),
                Path.Combine(currentDir, "Library", "PackageCache", $"{kPackageName}@*")
            };

            foreach (string upmPath in upmPaths)
            {
                if (Directory.Exists(upmPath) && File.Exists(Path.Combine(upmPath, "package.json")))
                {
                    return upmPath;
                }
            }

            // For .unitypackage installation (Assets folder)
            string assetsPath = Path.Combine(currentDir, "Assets", "UnityKnowLang");
            if (Directory.Exists(assetsPath))
            {
                return assetsPath;
            }

            // Fallback to Assets/UnityKnowLang
            return assetsPath;
        }

        public string GetStreamingAssetsPath(string packageRoot)
        {
            // In Editor, check both package locations
            string packageStreamingAssets = Path.Combine(packageRoot, "StreamingAssets");

            if (!Directory.Exists(packageStreamingAssets))
                Directory.CreateDirectory(packageStreamingAssets);
            
            return packageStreamingAssets;
        }

        public string GetTargetBinaryPath(string packageRoot)
        {
            string executableName = GetExecutableName();
            return Path.Combine(packageRoot, ".knowlang", executableName);
        }
    }
    #endregion

    #region Logger
    /// <summary>
    /// Handles logging operations for the KnowLang service
    /// </summary>
    public class KnowLangLogger : IDisposable
    {
        private StreamWriter logFileWriter;
        private bool isDisposed = false;

        public void Initialize(string logDirectory)
        {
            try
            {
                string logFilePath = Path.Combine(logDirectory, ".log");
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);
                logFileWriter = new StreamWriter(logFilePath, append: true) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to initialize logger: {ex.Message}");
            }
        }

        public void LogMessage(string message)
        {
            logFileWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        public void LogError(string error)
        {
            logFileWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {error}");
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            logFileWriter?.Close();
            logFileWriter?.Dispose();
            logFileWriter = null;
            isDisposed = true;
        }
    }
    #endregion

    #region Binary Manager
    /// <summary>
    /// Manages KnowLang binary extraction and availability with download-on-demand capability
    /// </summary>
    public class KnowLangBinaryManager
    {
        private readonly KnowLangPlatformHelper platformHelper;
        private readonly KnowLangLogger logger;
        
        // Configuration for GitHub releases
        private const string GITHUB_RELEASES_API = "https://api.github.com/repos";
        private KnowLangConfig knowlangConfig;

        public KnowLangBinaryManager(KnowLangPlatformHelper platformHelper, KnowLangLogger logger)
        {
            this.platformHelper = platformHelper;
            this.logger = logger;
            this.knowlangConfig = new KnowLangConfig();
        }

        public async Task<bool> EnsureBinariesAsync()
        {
            string packageRoot = platformHelper.GetPackageRoot();
            string targetBinaryPath = platformHelper.GetTargetBinaryPath(packageRoot);
            
            // Check if binaries already exist
            if (File.Exists(targetBinaryPath))
            {
                logger.LogMessage($"✅ KnowLang binaries found at: {targetBinaryPath}");
                return true;
            }

            logger.LogMessage("KnowLang binaries not found. Checking for cached archive...");

            string archivePath = FindLocalArchive(packageRoot);
            
            // If still not found, try to download
            if (string.IsNullOrEmpty(archivePath))
            {
                logger.LogMessage("No cached archive found. Downloading platform-specific binary...");
                archivePath = await DownloadBinaryArchiveAsync();
            }

            if (string.IsNullOrEmpty(archivePath))
            {
                logger.LogError("Failed to obtain KnowLang binary archive. Please check your internet connection or ensure the package is properly installed.");
                return false;
            }

            logger.LogMessage($"Found binary archive: {archivePath}");

            // Extract the archive
            return await ExtractBinaryArchiveAsync(archivePath, packageRoot);
        }

        public string FindServiceExecutable()
        {
            string packageRoot = platformHelper.GetPackageRoot();
            string targetPath = platformHelper.GetTargetBinaryPath(packageRoot);
            
            if (File.Exists(targetPath))
            {
                return targetPath;
            }

            logger.LogError($"Service executable not found at: {targetPath}");
            return null;
        }

        private string FindLocalArchive(string packageRoot)
        {
            string streamingAssetsPath = platformHelper.GetStreamingAssetsPath(packageRoot);
            // prepend a dot to prevent Unity from creating metadata files
            string localArchive = Path.Combine(streamingAssetsPath, '.' + platformHelper.PlatformArchiveFile);
            
            if (File.Exists(localArchive))
            {
                logger.LogMessage($"Found local archive: {localArchive}");
                return localArchive;
            }

            return null;
        }

        private async Task<string> DownloadBinaryArchiveAsync()
        {
            string filename = platformHelper.PlatformArchiveFile;

            // First, get the release info to find the download URL
            string releaseUrl = await GetReleaseDownloadUrlAsync(filename);
            if (string.IsNullOrEmpty(releaseUrl))
            {
                logger.LogError($"Could not find download URL for {filename}");
                return null;
            }

            string streamingAssetsPath = platformHelper.GetStreamingAssetsPath(platformHelper.GetPackageRoot());
            string filePath = Path.Combine(streamingAssetsPath, filename);

            logger.LogMessage($"Downloading {filename} from GitHub releases...");
            await DownloadFileAsync(releaseUrl, filePath);

            return filePath;
        }

        private async Task<string> GetReleaseDownloadUrlAsync(string filename)
        {
            string apiUrl = $"{GITHUB_RELEASES_API}/{knowlangConfig.githubRepository}/releases/tags/v{knowlangConfig.packageVersion}";

            using (var request = UnityWebRequest.Get(apiUrl))
            {
                request.timeout = knowlangConfig.apiTimeoutSeconds;
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var releaseInfo = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text);

                    // Find the asset with matching name
                    foreach (var asset in releaseInfo.assets)
                    {
                        if (asset.name == filename)
                        {
                            return asset.browser_download_url;
                        }
                    }
                }
                else
                {
                    logger.LogError($"Failed to fetch release info: {request.error}");
                    throw new Exception($"GitHub API request failed with status {request.result}: {request.error}");
                }

                return null;
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = knowlangConfig.downloadTimeoutSeconds;
                var operation = request.SendWebRequest();
                
                // Show progress with Unity's progress bar
                float lastProgress = 0;
                string filename = Path.GetFileName(destinationPath);
                
                try
                {
                    while (!operation.isDone)
                    {
                        float currentProgress = request.downloadProgress;
                        if (currentProgress > lastProgress + 0.01f) // Update more frequently for smoother UI
                        {
                            lastProgress = currentProgress;
                            string progressText = $"Downloading {filename}... {currentProgress * 100:F1}%";
                            EditorUtility.DisplayProgressBar("KnowLang Setup", progressText, currentProgress);
                            logger.LogMessage($"Download progress: {currentProgress * 100:F1}%");
                        }
                        await Task.Delay(50); // Reduced delay for more responsive UI
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        EditorUtility.DisplayProgressBar("KnowLang Setup", "Saving downloaded file...", 1.0f);
                        // Write the downloaded data to file
                        File.WriteAllBytes(destinationPath, request.downloadHandler.data);
                        logger.LogMessage("Download completed successfully");
                    }
                    else
                    {
                        logger.LogError($"Download failed: {request.error}");
                        throw new Exception($"Download failed with status {request.result}: {request.error}");
                    }
                }
                finally
                {
                    // Always clear the progress bar
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private async Task<bool> ExtractBinaryArchiveAsync(string archivePath, string packageRoot)
        {
            try
            {
                logger.LogMessage($"Extracting KnowLang binaries from: {archivePath}");
                
                // Show extraction progress
                EditorUtility.DisplayProgressBar("KnowLang Setup", "Extracting binaries...", 0.5f);

                string extractionTarget = Path.Combine(packageRoot, ".knowlang");

                // Create target directory if it doesn't exist
                if (Directory.Exists(extractionTarget))
                {
                    // Clean existing directory to avoid conflicts
                    Directory.Delete(extractionTarget, true);
                }
                Directory.CreateDirectory(extractionTarget);

                await Task.Run(() => RunCommand("tar", $"-xzf \"{archivePath}\" -C \"{extractionTarget}\""));

                EditorUtility.DisplayProgressBar("KnowLang Setup", "Extraction completed!", 1.0f);
                await Task.Delay(500); // Brief pause to show completion
                
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to extract binary archive: {ex.Message}");
                return false;
            }
            finally
            {
                // Always clear the progress bar
                EditorUtility.ClearProgressBar();
            }
        }

        private void RunCommand(string command, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    if (!string.IsNullOrEmpty(output))
                        logger.LogMessage($"Command output: {output}");
                }
                else
                {
                    logger.LogError($"Command failed (exit code {process.ExitCode}): {error}");
                    throw new Exception($"Command '{command} {arguments}' failed with exit code {process.ExitCode}: {error}");
                }
            }
        }
    }
    #endregion

    #region Process Manager
    /// <summary>
    /// Manages the Python process lifecycle for the KnowLang service
    /// </summary>
    public class KnowLangProcessManager : IDisposable
    {
        private Process pythonProcess;
        private readonly KnowLangLogger logger;
        private bool isDisposed = false;

        public KnowLangProcessManager(KnowLangLogger logger)
        {
            this.logger = logger;
        }

        public bool StartProcess(string executablePath, ServiceConfig config)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = config.GetCommandLineArgs(),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath)
                };

                pythonProcess = new Process { StartInfo = startInfo };
                
                // Subscribe to output events
                pythonProcess.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        logger.LogMessage($"[Python] {e.Data}");
                };
                
                pythonProcess.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        logger.LogError($"[Python Error] {e.Data}");
                };

                pythonProcess.Start();
                pythonProcess.BeginOutputReadLine();
                pythonProcess.BeginErrorReadLine();

                logger.LogMessage($"Python service process started (PID: {pythonProcess.Id})");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to start Python process: {ex.Message}");
                return false;
            }
        }

        public void StopProcess()
        {
            if (IsProcessRunning)
            {
                try
                {
                    pythonProcess.Kill();
                    pythonProcess.WaitForExit(5000); // Wait up to 5 seconds
                    pythonProcess.Dispose();
                    pythonProcess = null;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error stopping Python process: {ex.Message}");
                }
            }
        }

        public bool IsProcessRunning => pythonProcess != null && !pythonProcess.HasExited;

        public void Dispose()
        {
            if (isDisposed) return;
            
            StopProcess();
            isDisposed = true;
        }
    }
    #endregion

    #region Health Monitor
    /// <summary>
    /// Monitors service health and readiness
    /// </summary>
    public class KnowLangHealthMonitor : IDisposable
    {
        private readonly KnowLangLogger logger;
        private UnityWebRequest healthCheckRequest;
        private bool isDisposed = false;

        public KnowLangHealthMonitor(KnowLangLogger logger)
        {
            this.logger = logger;
        }

        public async Task<bool> CheckServiceHealthAsync(string serviceUrl)
        {
            try
            {
                using (var request = UnityWebRequest.Get($"{serviceUrl}/health"))
                {
                    request.timeout = 5;
                    
                    var operation = request.SendWebRequest();
                    
                    // Wait for completion
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = request.downloadHandler.text;
                        var healthData = JsonUtility.FromJson<KnowLangHealthResponse>(response);
                        return healthData?.status == "healthy";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Health check failed: {ex.Message}");
            }
            
            return false;
        }

        public async Task<bool> WaitForServiceReady(string serviceUrl, KnowLangProcessManager processManager, int timeoutSeconds = 60)
        {
            logger.LogMessage("Waiting for service to be ready...");
            
            for (int i = 0; i < timeoutSeconds; i++)
            {
                if (await CheckServiceHealthAsync(serviceUrl))
                {
                    return true;
                }
                
                await Task.Delay(1000);
                
                // Check if process is still running
                if (!processManager.IsProcessRunning)
                {
                    logger.LogError("Python process exited unexpectedly");
                    return false;
                }
            }
            
            return false;
        }

        public void CancelHealthCheck()
        {
            healthCheckRequest?.Abort();
            healthCheckRequest?.Dispose();
            healthCheckRequest = null;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            CancelHealthCheck();
            isDisposed = true;
        }
    }
    #endregion

    #region Main Server Manager
    /// <summary>
    /// Manages the lifecycle of the Python FastAPI service for Unity integration
    /// Supports both .unitypackage and UPM installation by auto-extracting binaries
    /// </summary>
    public class KnowLangServerManager : IDisposable
    {
        #region Events
        public event System.Action<ServiceStatus> OnStatusChanged;
        #endregion

        #region Properties
        public ServiceStatus Status { get; private set; } = ServiceStatus.Stopped;
        public string ServiceUrl => $"http://{config.Host}:{config.Port}{config.ApiPrefix}";
        public bool IsRunning => Status == ServiceStatus.Running;
        #endregion

        #region Private Fields
        private readonly ServiceConfig config;
        private readonly KnowLangPlatformHelper platformHelper;
        private readonly KnowLangLogger logger;
        private readonly KnowLangBinaryManager binaryManager;
        private readonly KnowLangProcessManager processManager;
        private readonly KnowLangHealthMonitor healthMonitor;
        private bool isDisposed = false;
        #endregion

        #region Constructor & Disposal
        public KnowLangServerManager(ServiceConfig config = null)
        {
            this.config = config ?? new ServiceConfig();
            
            // Initialize all managers
            this.platformHelper = new KnowLangPlatformHelper();
            this.logger = new KnowLangLogger();
            this.binaryManager = new KnowLangBinaryManager(platformHelper, logger);
            this.processManager = new KnowLangProcessManager(logger);
            this.healthMonitor = new KnowLangHealthMonitor(logger);
            
            // Subscribe to Unity events
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            logger.Initialize(platformHelper.GetPackageRoot());
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            // Unsubscribe from Unity events
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            
            // Stop service and dispose managers
            StopService();
            
            healthMonitor?.Dispose();
            processManager?.Dispose();
            logger?.Dispose();
            
            isDisposed = true;
        }
        #endregion

        #region Service Lifecycle
        public async Task<bool> StartServiceAsync()
        {
            if (Status == ServiceStatus.Running || Status == ServiceStatus.Starting)
            {
                logger.LogMessage("Service is already running or starting");
                return true;
            }

            try
            {

                SetStatus(ServiceStatus.Starting);
                logger.LogMessage("Starting KnowLang Python service...");

                // Ensure binaries are extracted and available
                if (!await binaryManager.EnsureBinariesAsync())
                {
                    logger.LogError("Failed to prepare KnowLang binaries");
                    SetStatus(ServiceStatus.Error);
                    return false;
                }

                // Find the Python service executable
                string executablePath = binaryManager.FindServiceExecutable();
                if (string.IsNullOrEmpty(executablePath))
                {
                    logger.LogError("Python service executable not found after extraction. Please check the archive contents.");
                    SetStatus(ServiceStatus.Error);
                    return false;
                }


                // Configure the YAML files before starting the service
                var configManager = new KnowLangConfigManager();
                configManager.ConfigureYamlFiles(executablePath);

                // Start the Python process
                if (!processManager.StartProcess(executablePath, config))
                {
                    SetStatus(ServiceStatus.Error);
                    return false;
                }

                // Wait for service to be ready
                bool isReady = await healthMonitor.WaitForServiceReady(ServiceUrl, processManager);
                if (isReady)
                {
                    SetStatus(ServiceStatus.Running);
                    logger.LogMessage($"✅ KnowLang service started successfully at {ServiceUrl}");
                    return true;
                }
                else
                {
                    SetStatus(ServiceStatus.Error);
                    logger.LogError("Service failed to start within timeout period");
                    StopService();
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to start service: {ex.Message}");
                SetStatus(ServiceStatus.Error);
                return false;
            }
        }

        public void StopService()
        {
            if (Status == ServiceStatus.Stopped || Status == ServiceStatus.Stopping)
                return;

            try
            {
                SetStatus(ServiceStatus.Stopping);
                logger.LogMessage("Stopping KnowLang service...");

                // Cancel health check if running
                healthMonitor.CancelHealthCheck();

                // Stop Python process
                processManager.StopProcess();

                SetStatus(ServiceStatus.Stopped);
                logger.LogMessage("✅ KnowLang service stopped");
            }
            catch (Exception ex)
            {
                logger.LogError($"Error stopping service: {ex.Message}");
                SetStatus(ServiceStatus.Error);
            }
        }

        public async Task<bool> RestartServiceAsync()
        {
            logger.LogMessage("Restarting KnowLang service...");
            StopService();
            await Task.Delay(1000); // Brief pause
            return await StartServiceAsync();
        }
        #endregion

        #region Service Health Monitoring
        public async Task<bool> CheckServiceHealthAsync()
        {
            return await healthMonitor.CheckServiceHealthAsync(ServiceUrl);
        }
        #endregion

        #region Unity Event Handlers
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Do nothing for now
        }

        private void OnBeforeAssemblyReload()
        {
            // Stop the service during assembly reload to prevent port conflicts
            logger.LogMessage("Assembly reload detected - stopping service to prevent port conflicts");
            
            // Force stop the Python process without changing status events
            // since the manager instance will be destroyed anyway
            processManager.StopProcess();
        }
        #endregion

        #region Status Management
        private void SetStatus(ServiceStatus newStatus)
        {
            if (Status != newStatus)
            {
                Status = newStatus;
                OnStatusChanged?.Invoke(Status);
            }
        }
        #endregion
    }
    #endregion

    #region KnowLang Configuration Manager
    /// <summary>
    /// Manages the codebase.yaml configuration file for KnowLang service
    /// </summary>
    public class KnowLangConfigManager
    {
        /// <summary>
        /// Configures the *.yaml files to point to Unity's Assets directory
        /// </summary>
        /// <param name="executablePath">Path to the KnowLang service executable</param>
        /// <returns>True if configuration was successful, false otherwise</returns>
        public bool ConfigureYamlFiles(string executablePath)
        {
            try
            {
                List<string> configPaths = FindSettingFiles(executablePath);
                if (configPaths.Count == 0)
                {
                    UnityEngine.Debug.LogWarning("codebase.yaml file not found near the service executable.");
                    return true;
                }

                // Get Unity's Assets folder path
                string assetsPath = Application.dataPath;

                // Read, modify, and write back the configuration
                foreach (string configPath in configPaths)
                {
                    string yamlContent = File.ReadAllText(configPath);
                    string modifiedYaml = UpdateProcessorConfigPath(yamlContent, assetsPath);

                    File.WriteAllText(configPath, modifiedYaml);
                }

                UnityEngine.Debug.Log($"✅ Updated YAML files processor_config directory_path to: {assetsPath}");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to configure YAML files: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Finds the codebase.yaml file near the service executable
        /// </summary>
        /// <param name="executablePath">Path to the service executable</param>
        /// <returns>Path to codebase.yaml file or null if not found</returns>
        private List<string> FindSettingFiles(string executablePath)
        {
            string executableDir = Path.GetDirectoryName(executablePath);
            var configPaths = new List<string>();

            // Check in the same directory as the executable
            string configPath = Path.Combine(executableDir, "_internal", "settings");
            if (Directory.Exists(configPath))
            {
                // Support both .yaml and .yml extensions using multiple patterns
                string[] yamlPatterns = { "*.yaml", "*.yml" };
                foreach (string pattern in yamlPatterns)
                {
                    configPaths.AddRange(Directory.GetFiles(configPath, pattern, SearchOption.AllDirectories));
                }
            }

            return configPaths;
        }
        
        /// <summary>
        /// Updates the processor_config directory_path in the YAML content by replacing placeholders
        /// </summary>
        /// <param name="yamlContent">Original YAML content</param>
        /// <param name="assetsPath">Unity's Assets directory absolute path</param>
        /// <returns>Modified YAML content</returns>
        private string UpdateProcessorConfigPath(string yamlContent, string assetsPath)
        {
            // Normalize the path for YAML (use forward slashes)
            string normalizedPath = assetsPath.Replace('\\', '/');
            
            // Define placeholders to look for
            string placeholder = "%UNITY_ASSETS_PATH%";

            string result = yamlContent;
            bool replacementMade = false;
            
            // Replace any found placeholders with the actual Assets path
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, $"{normalizedPath}");
                replacementMade = true;
                UnityEngine.Debug.Log($"🔄 Replaced placeholder '{placeholder}' with: {normalizedPath}");
            }
            
            // If no placeholders were found, log a warning
            if (!replacementMade)
            {
                UnityEngine.Debug.LogWarning("⚠️ No Unity Assets path placeholders found in YAML files. Expected placeholders: %UNITY_ASSETS_PATH%, etc.");
            }
            
            return result;
        }
    }
    #endregion

    #region Supporting Types
    public enum ServiceStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }

    [Serializable]
    public class ServiceConfig
    {
        public string Host = "127.0.0.1";
        public int Port = 8080;
        public string ApiPrefix = "/api/v1";
        public bool AutoStart = true;
        public bool RestartOnPlayMode = false;
        public int HealthCheckInterval = 30; // seconds

        public string GetCommandLineArgs()
        {
            var args = new List<string>
            {
                $"--port={Port}",
            };

            return string.Join(" ", args);  
        }
    }

    [Serializable]
    public class KnowLangHealthResponse
    {
        public string status;
        public string service;
    }

    // Supporting data structures for GitHub API
    [Serializable]
    public class GitHubRelease
    {
        public string tag_name;
        public string name;
        public GitHubAsset[] assets;
    }

    [Serializable]
    public class GitHubAsset
    {
        public string name;
        public string browser_download_url;
        public int size;
    }
    #endregion
}