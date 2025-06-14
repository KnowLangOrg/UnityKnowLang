// Assets/UnityKnowLang/Editor/Scripts/KnowLangServerManger.cs
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
    /// </summary>
    public class KnowLangServerManger : IDisposable
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
        public KnowLangServerManger(ServiceConfig config = null)
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

                // Find the Python service executable
                string executablePath = FindServiceExecutable();
                if (string.IsNullOrEmpty(executablePath))
                {
                    LogError("Python service executable not found. Please ensure the KnowLang package is properly installed.");
                    SetStatus(ServiceStatus.Error);
                    return false;
                }

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

        private async Task<bool> WaitForServiceReady(int timeoutSeconds = 30)
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
            // Get the platform-specific executable path
            string platform = GetPlatformFolder();
            string executableName = GetExecutableName();
            
            string streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "KnowLang", platform, executableName);
            
            if (File.Exists(streamingAssetsPath))
            {
                return streamingAssetsPath;
            }

            // Fallback: check if we're in development mode
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            // prefix the . so that Unity can ignore it
            string devPath = Path.Combine(projectRoot, "Assets", "UnityKnowLang", ".StreamingAssets", "KnowLang", platform, executableName);
            
            if (File.Exists(devPath))
            {
                return devPath;
            }

            LogError($"Service executable not found at expected paths:\n- {streamingAssetsPath}\n- {devPath}");
            return null;
        }

        private bool StartPythonProcess(string executablePath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "",
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

        private string GetPlatformFolder()
        {
            #if UNITY_EDITOR_WIN
                return "Windows";
            #elif UNITY_EDITOR_OSX
                return "macOS";
            #elif UNITY_EDITOR_LINUX
                return "Linux";
            #else
                return "Unknown";
            #endif
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
            // Optionally restart service when entering/exiting play mode
            if (config.RestartOnPlayMode)
            {
                switch (state)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        LogMessage("Exiting edit mode - maintaining service");
                        break;
                    case PlayModeStateChange.EnteredPlayMode:
                        LogMessage("Entered play mode - service available");
                        break;
                }
            }
        }

        private void OnBeforeAssemblyReload()
        {
            // Keep service running during assembly reload for hot-reload support
            LogMessage("Assembly reload detected - maintaining service connection");
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
            logFileWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {error}");
        }
        #endregion
    }

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
    }

    [Serializable]
    public class KnowLangHealthResponse
    {
        public string status;
        public string service;
    }
    #endregion
}