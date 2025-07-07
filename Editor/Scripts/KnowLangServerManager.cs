using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityKnowLang.Editor
{
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
        private Process pythonProcess;
        private readonly ServiceConfig config;
        private UnityWebRequest healthCheckRequest;
        private bool isDisposed = false;
        private StreamWriter logFileWriter;
        #endregion

        #region Constructor & Disposal
        public KnowLangServerManager(ServiceConfig config = null)
        {
            this.config = config ?? new ServiceConfig();
            
            // Subscribe to Unity events
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            
            // Unsubscribe from Unity events
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            
            // Stop service
            StopService();
            
            isDisposed = true;
        }
        #endregion

        #region Service Lifecycle
        public async Task<bool> StartServiceAsync()
        {
            if (Status == ServiceStatus.Running || Status == ServiceStatus.Starting)
            {
                LogMessage("Service is already running or starting");
                return true;
            }

            try
            {
                SetStatus(ServiceStatus.Starting);
                LogMessage("Starting KnowLang Python service...");

                // Ensure binaries are extracted and available
                if (!await EnsureBinariesAsync())
                {
                    LogError("Failed to prepare KnowLang binaries");
                    SetStatus(ServiceStatus.Error);
                    return false;
                }

                // Find the Python service executable
                string executablePath = FindServiceExecutable();
                if (string.IsNullOrEmpty(executablePath))
                {
                    LogError("Python service executable not found after extraction. Please check the archive contents.");
                    SetStatus(ServiceStatus.Error);
                    return false;
                }

                // Configure the YAML files before starting the service
                var configManager = new KnowLangConfigManager();
                configManager.ConfigureYamlFiles(executablePath);

                // Start the Python process
                if (!StartPythonProcess(executablePath))
                {
                    SetStatus(ServiceStatus.Error);
                    return false;
                }

                // Wait for service to be ready
                bool isReady = await WaitForServiceReady();
                if (isReady)
                {
                    SetStatus(ServiceStatus.Running);
                    LogMessage($"✅ KnowLang service started successfully at {ServiceUrl}");
                    return true;
                }
                else
                {
                    SetStatus(ServiceStatus.Error);
                    LogError("Service failed to start within timeout period");
                    StopService();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to start service: {ex.Message}");
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
                LogMessage("Stopping KnowLang service...");

                // Cancel health check if running
                healthCheckRequest?.Abort();
                healthCheckRequest?.Dispose();
                healthCheckRequest = null;

                // Stop Python process
                if (pythonProcess != null && !pythonProcess.HasExited)
                {
                    pythonProcess.Kill();
                    pythonProcess.WaitForExit(5000); // Wait up to 5 seconds
                    pythonProcess.Dispose();
                    pythonProcess = null;
                }

                logFileWriter?.Close();
                logFileWriter?.Dispose();
                logFileWriter = null;

                SetStatus(ServiceStatus.Stopped);
                LogMessage("✅ KnowLang service stopped");
            }
            catch (Exception ex)
            {
                LogError($"Error stopping service: {ex.Message}");
                SetStatus(ServiceStatus.Error);
            }
        }

        public async Task<bool> RestartServiceAsync()
        {
            LogMessage("Restarting KnowLang service...");
            StopService();
            await Task.Delay(1000); // Brief pause
            return await StartServiceAsync();
        }
        #endregion

        #region Binary Management - THE GENIUS SOLUTION! 🧠✨
        /// <summary>
        /// Ensures KnowLang binaries are available by extracting from StreamingAssets if needed
        /// This handles both .unitypackage and UPM installation methods
        /// </summary>
        private async Task<bool> EnsureBinariesAsync()
        {
            try
            {
                string targetBinaryPath = GetTargetBinaryPath();
                
                // Check if binaries already exist (e.g., from UPM with .knowlang folder)
                if (File.Exists(targetBinaryPath))
                {
                    LogMessage($"✅ KnowLang binaries found at: {targetBinaryPath}");
                    return true;
                }

                LogMessage("KnowLang binaries not found. Checking for archive in StreamingAssets...");

                // Look for archive in StreamingAssets
                string archivePath = FindBinaryArchive();
                if (string.IsNullOrEmpty(archivePath))
                {
                    LogError("No KnowLang binary archive found in StreamingAssets. Please ensure the package is properly installed.");
                    return false;
                }

                LogMessage($"Found binary archive: {archivePath}");

                // Extract the archive
                return await ExtractBinaryArchiveAsync(archivePath);
            }
            catch (Exception ex)
            {
                LogError($"Error ensuring binaries: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the target path where the main binary should be located
        /// </summary>
        private string GetTargetBinaryPath()
        {
            string packageRoot = GetPackageRoot();
            string executableName = GetExecutableName();
            return Path.Combine(packageRoot, ".knowlang", executableName);
        }

        /// <summary>
        /// Gets the root directory of the UnityKnowLang package
        /// </summary>
        private string GetPackageRoot()
        {
            // Try to find the package root by looking for package.json
            string currentDir = Path.GetDirectoryName(Application.dataPath);
            
            // For UPM packages
            string[] upmPaths = {
                Path.Combine(currentDir, "Packages", "com.knowlang.unityknowlang"),
                Path.Combine(currentDir, "Library", "PackageCache", "com.knowlang.unityknowlang@*")
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

        /// <summary>
        /// Finds the binary archive in StreamingAssets
        /// </summary>
        private string FindBinaryArchive()
        {
            string streamingAssetsPath = GetStreamingAssetsPath();
            
            if (!Directory.Exists(streamingAssetsPath))
            {
                LogMessage($"StreamingAssets directory not found: {streamingAssetsPath}");
                return null;
            }

            // Look for platform-specific archives first
            string platformSpecific = Path.Combine(streamingAssetsPath, $"knowlang-{GetPlatformName()}.tar.gz");
            if (File.Exists(platformSpecific))
            {
                return platformSpecific;
            }

            // Look for generic archives
            string[] archivePatterns = {
                "knowlang-*.tar.gz",
                "knowlang.tar.gz",
                "*.tar.gz"
            };

            foreach (string pattern in archivePatterns)
            {
                string[] matchingFiles = Directory.GetFiles(streamingAssetsPath, pattern);
                if (matchingFiles.Length > 0)
                {
                    return matchingFiles[0]; // Return first match
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the StreamingAssets path for the current context
        /// </summary>
        private string GetStreamingAssetsPath()
        {
            // In Editor, check both package locations
            string packageRoot = GetPackageRoot();
            string packageStreamingAssets = Path.Combine(packageRoot, "StreamingAssets");
            
            if (Directory.Exists(packageStreamingAssets))
            {
                return packageStreamingAssets;
            }

            // Fallback to Unity's default StreamingAssets
            return Path.Combine(Application.dataPath, "StreamingAssets");
        }

        /// <summary>
        /// Extracts the binary archive using native OS commands to preserve symbolic links
        /// </summary>
        private async Task<bool> ExtractBinaryArchiveAsync(string archivePath)
        {
            try
            {
                LogMessage($"Extracting KnowLang binaries from: {archivePath}");

                string packageRoot = GetPackageRoot();
                string extractionTarget = Path.Combine(packageRoot, ".knowlang");

                // Create target directory if it doesn't exist
                if (Directory.Exists(extractionTarget))
                {
                    // Clean existing directory to avoid conflicts
                    Directory.Delete(extractionTarget, true);
                }
                Directory.CreateDirectory(extractionTarget);

                // Extract using platform-specific commands
                bool success = await ExtractArchiveNative(archivePath, extractionTarget);
                
                if (!success)
                {
                    LogError("Native extraction failed. Please ensure tar command is available on your system.");
                    return false;
                }

                // Verify extraction
                string targetBinaryPath = GetTargetBinaryPath();
                if (File.Exists(targetBinaryPath))
                {
                    LogMessage($"✅ Successfully extracted KnowLang binaries to: {extractionTarget}");
                    return true;
                }
                else
                {
                    LogError($"Extraction completed but binary not found at expected location: {targetBinaryPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to extract binary archive: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts archive using native OS commands (preserves symlinks)
        /// </summary>
        private async Task<bool> ExtractArchiveNative(string archivePath, string extractPath)
        {
            try
            {
                return await Task.Run(() =>
                {
#if UNITY_EDITOR_WIN
                    return ExtractOnWindows(archivePath, extractPath);
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                    return ExtractOnUnix(archivePath, extractPath);
#else
                    LogError("Unsupported platform for native extraction");
                    return false;
#endif
                });
            }
            catch (Exception ex)
            {
                LogError($"Native extraction failed: {ex.Message}");
                return false;
            }
        }

#if UNITY_EDITOR_WIN
        /// <summary>
        /// Windows extraction using tar command (Windows 10+ has built-in tar)
        /// </summary>
        private bool ExtractOnWindows(string archivePath, string extractPath)
        {
            try
            {
                // Try Windows 10+ built-in tar first
                if (RunCommand("tar", $"-xzf \"{archivePath}\" -C \"{extractPath}\""))
                {
                    LogMessage("✅ Extracted using Windows built-in tar");
                    return true;
                }

                LogError("Windows extraction failed. Please ensure you're using Windows 10+ which supports tar command natively.");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Windows extraction failed: {ex.Message}");
                return false;
            }
        }

#endif

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        /// <summary>
        /// Unix extraction using tar command (preserves symlinks perfectly)
        /// </summary>
        private bool ExtractOnUnix(string archivePath, string extractPath)
        {
            try
            {
                // Use tar command - handles symlinks perfectly
                bool success = RunCommand("tar", $"-xzf \"{archivePath}\" -C \"{extractPath}\"");
                
                if (success)
                {
                    LogMessage("✅ Extracted using Unix tar (symlinks preserved)");
                    return true;
                }

                LogError("tar command failed");
                return false;
            }
            catch (Exception ex)
            {
                LogError($"Unix extraction failed: {ex.Message}");
                return false;
            }
        }
#endif

        /// <summary>
        /// Runs a command and waits for completion
        /// </summary>
        private bool RunCommand(string command, string arguments)
        {
            try
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
                            LogMessage($"Command output: {output}");
                        return true;
                    }
                    else
                    {
                        LogError($"Command failed (exit code {process.ExitCode}): {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to run command '{command} {arguments}': {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Gets the platform name for archive selection
        /// </summary>
        private string GetPlatformName()
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
        #endregion

        #region Service Health Monitoring
        public async Task<bool> CheckServiceHealthAsync()
        {
            try
            {
                using (var request = UnityWebRequest.Get($"{ServiceUrl}/health"))
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
                LogError($"Health check failed: {ex.Message}");
            }
            
            return false;
        }

        private async Task<bool> WaitForServiceReady(int timeoutSeconds = 60)
        {
            LogMessage("Waiting for service to be ready...");
            
            for (int i = 0; i < timeoutSeconds; i++)
            {
                if (await CheckServiceHealthAsync())
                {
                    return true;
                }
                
                await Task.Delay(1000);
                
                // Check if process is still running
                if (pythonProcess?.HasExited == true)
                {
                    LogError("Python process exited unexpectedly");
                    return false;
                }
            }
            
            return false;
        }
        #endregion

        #region Python Process Management
        private string FindServiceExecutable()
        {
            string targetPath = GetTargetBinaryPath();
            
            if (File.Exists(targetPath))
            {
                return targetPath;
            }

            LogError($"Service executable not found at: {targetPath}");
            return null;
        }

        private bool StartPythonProcess(string executablePath)
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

                string logFilePath = Path.Combine(Path.GetDirectoryName(executablePath), "log.txt");
                logFileWriter = new StreamWriter(logFilePath, append: true) { AutoFlush = true };
                
                // Subscribe to output events
                pythonProcess.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        LogMessage($"[Python] {e.Data}");
                };
                
                pythonProcess.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        LogError($"[Python Error] {e.Data}");
                };

                pythonProcess.Start();
                pythonProcess.BeginOutputReadLine();
                pythonProcess.BeginErrorReadLine();

                LogMessage($"Python service process started (PID: {pythonProcess.Id})");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Failed to start Python process: {ex.Message}");
                return false;
            }
        }

        private string GetExecutableName()
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
        #endregion

        #region Unity Event Handlers
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Do nothing for now
        }

        private void OnBeforeAssemblyReload()
        {
            // Stop the service during assembly reload to prevent port conflicts
            LogMessage("Assembly reload detected - stopping service to prevent port conflicts");
            
            // Force stop the Python process without changing status events
            // since the manager instance will be destroyed anyway
            if (pythonProcess != null && !pythonProcess.HasExited)
            {
                try
                {
                    LogMessage($"Killing Python process (PID: {pythonProcess.Id}) before assembly reload");
                    pythonProcess.Kill();
                    pythonProcess.WaitForExit(2000); // Wait up to 2 seconds
                    pythonProcess.Dispose();
                    pythonProcess = null;
                }
                catch (Exception ex)
                {
                    LogError($"Error killing Python process during assembly reload: {ex.Message}");
                }
            }
            
            // Clean up log file writer
            logFileWriter?.Close();
            logFileWriter?.Dispose();
            logFileWriter = null;
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

        private void LogMessage(string message)
        {
            logFileWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        private void LogError(string error)
        {
            logFileWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {error}");
        }
        #endregion
    }

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
            string configPath = Path.Combine(executableDir, "_internal", "settings", "assets");
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
                $"--server.port={Port}",
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
    #endregion
}