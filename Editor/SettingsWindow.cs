using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace UnityKnowLang.Editor
{
    public class SettingsWindow : EditorWindow
    {
        [SerializeField] private KnowLangSettings settings = new KnowLangSettings();
        
        private TextField serviceHostField;
        private IntegerField servicePortField;
        private Toggle autoStartToggle;
        private TextField configPathField;
        private TextField backendUrlField;
        private IntegerField timeoutField;
        private Toggle enableLoggingToggle;
        private PopupField<string> logLevelPopup;
        private TextField pythonPathField;
        private Button browseButton;
        private Button browseConfigButton;
        private Button testConnectionButton;
        private Button saveButton;
        private Button resetButton;
        private Label connectionStatusLabel;
        private VisualElement advancedSection;
        private Toggle showAdvancedToggle;
        
        [MenuItem("Window/UnityKnowLang/Settings")]
        public static void ShowWindow()
        {
            SettingsWindow window = GetWindow<SettingsWindow>();
            window.titleContent = new GUIContent("UnityKnowLang Settings");
            window.minSize = new Vector2(400, 500);
            window.maxSize = new Vector2(600, 800);
        }

        public void CreateGUI()
        {
            // Load existing settings
            LoadSettings();
            
            // Create main container with scroll view
            var scrollView = new ScrollView();
            rootVisualElement.Add(scrollView);
            
            var mainContainer = new VisualElement();
            scrollView.Add(mainContainer);
            
            // Create title
            CreateTitle(mainContainer);
            
            // Create service configuration section
            CreateServiceConfigSection(mainContainer);
            
            // Create connection settings section
            CreateConnectionSection(mainContainer);
            
            // Create service settings section  
            CreateServiceSection(mainContainer);
            
            // Create advanced settings section
            CreateAdvancedSection(mainContainer);
            
            // Create action buttons
            CreateActionButtons(mainContainer);
            
            // Create status section
            CreateStatusSection(mainContainer);
            
            // Bind values to UI
            BindSettingsToUI();
        }
        
        private void CreateTitle(VisualElement parent)
        {
            var titleLabel = new Label("UnityKnowLang Plugin Settings");
            titleLabel.style.fontSize = 18;
            titleLabel.style.marginBottom = 20;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            parent.Add(titleLabel);
        }
        
        private void CreateServiceConfigSection(VisualElement parent)
        {
            var section = CreateSection("Service Configuration", parent);
            
            // Service Host
            var hostContainer = CreateFieldContainer("Service Host:", section);
            serviceHostField = new TextField();
            serviceHostField.style.flexGrow = 1;
            serviceHostField.RegisterValueChangedCallback(OnSettingChanged);
            hostContainer.Add(serviceHostField);
            
            // Service Port
            var portContainer = CreateFieldContainer("Service Port:", section);
            servicePortField = new IntegerField();
            servicePortField.style.width = 100;
            servicePortField.RegisterValueChangedCallback(OnSettingChanged);
            portContainer.Add(servicePortField);
            
            // Auto-start service
            var autoStartContainer = CreateFieldContainer("Auto-start Service:", section);
            autoStartToggle = new Toggle();
            autoStartToggle.RegisterValueChangedCallback(OnSettingChanged);
            autoStartContainer.Add(autoStartToggle);
            
            // Config file path
            var configContainer = CreateFieldContainer("Config File:", section);
            configPathField = new TextField();
            configPathField.style.flexGrow = 1;
            configPathField.RegisterValueChangedCallback(OnSettingChanged);
            configContainer.Add(configPathField);
            
            browseConfigButton = new Button(BrowseConfigPath)
            {
                text = "Browse"
            };
            browseConfigButton.style.width = 80;
            browseConfigButton.style.marginLeft = 5;
            configContainer.Add(browseConfigButton);
        }
        
        private void CreateConnectionSection(VisualElement parent)
        {
            var section = CreateSection("Connection Settings", parent);
            
            // Backend URL (computed from host and port)
            var urlContainer = CreateFieldContainer("Backend URL:", section);
            backendUrlField = new TextField();
            backendUrlField.style.flexGrow = 1;
            backendUrlField.SetEnabled(false); // Read-only, computed from host:port
            urlContainer.Add(backendUrlField);
            
            // Timeout
            var timeoutContainer = CreateFieldContainer("Timeout (seconds):", section);
            timeoutField = new IntegerField();
            timeoutField.style.width = 100;
            timeoutField.RegisterValueChangedCallback(OnSettingChanged);
            timeoutContainer.Add(timeoutField);
            
            // Test connection button
            testConnectionButton = new Button(TestConnection)
            {
                text = "Test Connection"
            };
            testConnectionButton.style.marginTop = 10;
            section.Add(testConnectionButton);
        }
        
        private void CreateServiceSection(VisualElement parent)
        {
            var section = CreateSection("Service Settings", parent);
            
            // Python executable path
            var pythonPathContainer = CreateFieldContainer("Python Service Path:", section);
            pythonPathField = new TextField();
            pythonPathField.style.flexGrow = 1;
            pythonPathField.RegisterValueChangedCallback(OnSettingChanged);
            pythonPathContainer.Add(pythonPathField);
            
            browseButton = new Button(BrowsePythonPath)
            {
                text = "Browse"
            };
            browseButton.style.width = 80;
            browseButton.style.marginLeft = 5;
            pythonPathContainer.Add(browseButton);
            
            // Enable logging
            var loggingContainer = CreateFieldContainer("Enable Logging:", section);
            enableLoggingToggle = new Toggle();
            enableLoggingToggle.RegisterValueChangedCallback(OnSettingChanged);
            loggingContainer.Add(enableLoggingToggle);
            
            // Log level
            var logLevelContainer = CreateFieldContainer("Log Level:", section);
            logLevelPopup = new PopupField<string>(new List<string> { "Debug", "Info", "Warning", "Error" }, "Info");
            logLevelPopup.style.width = 150;
            logLevelPopup.RegisterValueChangedCallback(OnSettingChanged);
            logLevelContainer.Add(logLevelPopup);
        }
        
        private void CreateAdvancedSection(VisualElement parent)
        {
            // Advanced section toggle
            showAdvancedToggle = new Toggle("Show Advanced Settings");
            showAdvancedToggle.style.marginTop = 20;
            showAdvancedToggle.RegisterCallback<ChangeEvent<bool>>(OnAdvancedToggleChanged);
            parent.Add(showAdvancedToggle);
            
            // Advanced section container
            advancedSection = CreateSection("Advanced Settings", parent);
            advancedSection.style.display = DisplayStyle.None;
            
            // Database settings
            var dbSection = CreateSubSection("Database Settings", advancedSection);
            
            var dbPathContainer = CreateFieldContainer("Database Path:", dbSection);
            var dbPathField = new TextField();
            dbPathField.SetEnabled(false);
            dbPathContainer.Add(dbPathField);
            
            // API settings
            var apiSection = CreateSubSection("API Settings", advancedSection);
            
            var maxRetriesContainer = CreateFieldContainer("Max Retries:", apiSection);
            var maxRetriesField = new IntegerField();
            maxRetriesField.value = 3;
            maxRetriesField.style.width = 100;
            maxRetriesContainer.Add(maxRetriesField);
            
            var retryDelayContainer = CreateFieldContainer("Retry Delay (ms):", apiSection);
            var retryDelayField = new IntegerField();
            retryDelayField.value = 1000;
            retryDelayField.style.width = 100;
            retryDelayContainer.Add(retryDelayField);
            
            // Debug settings
            var debugSection = CreateSubSection("Debug Settings", advancedSection);
            
            var verboseLoggingContainer = CreateFieldContainer("Verbose Logging:", debugSection);
            var verboseLoggingToggle = new Toggle();
            verboseLoggingContainer.Add(verboseLoggingToggle);
            
            var saveRequestsContainer = CreateFieldContainer("Save API Requests:", debugSection);
            var saveRequestsToggle = new Toggle();
            saveRequestsContainer.Add(saveRequestsToggle);
            
            // Service info section
            var serviceInfoSection = CreateSubSection("Service Information", advancedSection);
            
            var platformLabel = new Label($"Platform: {GetCurrentPlatform()}");
            platformLabel.style.fontSize = 12;
            platformLabel.style.marginBottom = 2;
            serviceInfoSection.Add(platformLabel);
            
            var executableLabel = new Label($"Service Executable: {GetServiceExecutableName()}");
            executableLabel.style.fontSize = 12;
            executableLabel.style.marginBottom = 2;
            serviceInfoSection.Add(executableLabel);
            
            var streamingAssetsLabel = new Label($"StreamingAssets Path: {Application.streamingAssetsPath}");
            streamingAssetsLabel.style.fontSize = 12;
            streamingAssetsLabel.style.marginBottom = 2;
            serviceInfoSection.Add(streamingAssetsLabel);
        }
        
        private void CreateActionButtons(VisualElement parent)
        {
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.SpaceBetween;
            buttonContainer.style.marginTop = 30;
            
            // Reset to defaults
            resetButton = new Button(OnResetClicked)
            {
                text = "Reset to Defaults"
            };
            resetButton.style.width = 140;
            buttonContainer.Add(resetButton);
            
            // Right side buttons
            var rightButtons = new VisualElement();
            rightButtons.style.flexDirection = FlexDirection.Row;
            
            var cancelButton = new Button(() => Close())
            {
                text = "Cancel"
            };
            cancelButton.style.width = 80;
            cancelButton.style.marginRight = 10;
            rightButtons.Add(cancelButton);
            
            saveButton = new Button(OnSaveClicked)
            {
                text = "Save"
            };
            saveButton.style.width = 80;
            saveButton.SetEnabled(false); // Initially disabled
            rightButtons.Add(saveButton);
            
            buttonContainer.Add(rightButtons);
            parent.Add(buttonContainer);
        }
        
        private void CreateStatusSection(VisualElement parent)
        {
            var statusSection = new VisualElement();
            statusSection.style.marginTop = 20;
            statusSection.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            
            var statusTitle = new Label("Connection Status");
            statusTitle.style.marginBottom = 5;
            statusSection.Add(statusTitle);
            
            connectionStatusLabel = new Label("Not tested");
            connectionStatusLabel.style.color = Color.gray;
            statusSection.Add(connectionStatusLabel);
            
            parent.Add(statusSection);
        }
        
        private VisualElement CreateSection(string title, VisualElement parent)
        {
            var sectionTitle = new Label(title);
            sectionTitle.style.fontSize = 14;
            sectionTitle.style.marginTop = 20;
            sectionTitle.style.marginBottom = 10;
            parent.Add(sectionTitle);
            
            var section = new VisualElement();
            section.style.marginLeft = 10;
            parent.Add(section);
            
            return section;
        }
        
        private VisualElement CreateSubSection(string title, VisualElement parent)
        {
            var subSectionTitle = new Label(title);
            subSectionTitle.style.fontSize = 12;
            subSectionTitle.style.marginTop = 15;
            subSectionTitle.style.marginBottom = 8;
            subSectionTitle.style.color = new Color(0.8f, 0.8f, 0.8f);
            parent.Add(subSectionTitle);
            
            var subSection = new VisualElement();
            subSection.style.marginLeft = 10;
            parent.Add(subSection);
            
            return subSection;
        }
        
        private VisualElement CreateFieldContainer(string labelText, VisualElement parent)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 8;
            
            var label = new Label(labelText);
            label.style.width = 150;
            label.style.minWidth = 150;
            container.Add(label);
            
            parent.Add(container);
            return container;
        }
        
        private string GetCurrentPlatform()
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
        
        private string GetServiceExecutableName()
        {
            #if UNITY_EDITOR_WIN
                return "knowlang-unity-service.exe";
            #elif UNITY_EDITOR_OSX
                return "knowlang-unity-service";
            #elif UNITY_EDITOR_LINUX
                return "knowlang-unity-service";
            #else
                return "knowlang-unity-service";
            #endif
        }
        
        private void OnAdvancedToggleChanged(ChangeEvent<bool> evt)
        {
            advancedSection.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void OnSettingChanged<T>(ChangeEvent<T> evt)
        {
            // Enable save button when settings change
            if (saveButton != null)
                saveButton.SetEnabled(true);
                
            // Update computed backend URL when host or port changes
            if (evt.target == serviceHostField || evt.target == servicePortField)
            {
                if (backendUrlField != null && serviceHostField != null && servicePortField != null)
                {
                    backendUrlField.value = $"http://{serviceHostField.value}:{servicePortField.value}";
                }
            }
        }
        
        private async void TestConnection()
        {
            testConnectionButton.SetEnabled(false);
            testConnectionButton.text = "Testing...";
            connectionStatusLabel.text = "Testing connection...";
            connectionStatusLabel.style.color = Color.yellow;
            
            try
            {
                // Update backend URL from current host/port values
                string testUrl = $"http://{serviceHostField.value}:{servicePortField.value}";
                
                // Use actual service client to test connection
                var testClient = new ServiceClient(testUrl, timeoutField.value);
                var health = await testClient.GetAsync<HealthResponse>("/health");
                
                if (health?.status == "healthy")
                {
                    connectionStatusLabel.text = "✅ Connection successful!";
                    connectionStatusLabel.style.color = Color.green;
                }
                else
                {
                    connectionStatusLabel.text = "❌ Service responded but status is not healthy";
                    connectionStatusLabel.style.color = Color.red;
                }
            }
            catch (Exception ex)
            {
                connectionStatusLabel.text = $"❌ Connection failed: {ex.Message}";
                connectionStatusLabel.style.color = Color.red;
            }
            finally
            {
                testConnectionButton.SetEnabled(true);
                testConnectionButton.text = "Test Connection";
            }
        }
        
        private void BrowsePythonPath()
        {
            string extension = "";
            #if UNITY_EDITOR_WIN
                extension = "exe";
            #endif
            
            string path = EditorUtility.OpenFilePanel("Select Python Service Executable", 
                                                     Application.streamingAssetsPath, extension);
            if (!string.IsNullOrEmpty(path))
            {
                pythonPathField.value = path;
                OnSettingChanged(new ChangeEvent<string>());
            }
        }
        
        private void BrowseConfigPath()
        {
            string path = EditorUtility.OpenFilePanel("Select KnowLang Config File", 
                Application.dataPath, "json,toml,yaml,yml");
            
            if (!string.IsNullOrEmpty(path))
            {
                configPathField.value = path;
                OnSettingChanged(new ChangeEvent<string>());
            }
        }
        
        private void SaveSettings()
        {
            // Update settings from UI
            UpdateSettingsFromUI();
            
            // Save using the static method
            try
            {
                KnowLangSettings.SaveSettings(settings);
                
                connectionStatusLabel.text = "✅ Settings saved successfully";
                connectionStatusLabel.style.color = Color.green;
                
                // Disable save button until next change
                saveButton.SetEnabled(false);
                
                Debug.Log("UnityKnowLang settings saved");
            }
            catch (Exception ex)
            {
                connectionStatusLabel.text = $"❌ Failed to save settings: {ex.Message}";
                connectionStatusLabel.style.color = Color.red;
                Debug.LogError($"Failed to save settings: {ex}");
            }
        }
        
        private void LoadSettings()
        {
            settings = KnowLangSettings.LoadSettings();
        }
        
        private void BindSettingsToUI()
        {
            if (serviceHostField != null)
                serviceHostField.value = settings.serviceHost;
            if (servicePortField != null)
                servicePortField.value = settings.servicePort;
            if (autoStartToggle != null)
                autoStartToggle.value = settings.autoStartService;
            if (configPathField != null)
                configPathField.value = settings.configFilePath;
            if (backendUrlField != null)
                backendUrlField.value = $"http://{settings.serviceHost}:{settings.servicePort}";
            if (timeoutField != null)
                timeoutField.value = settings.timeoutSeconds;
            if (enableLoggingToggle != null)
                enableLoggingToggle.value = settings.enableLogging;
            if (logLevelPopup != null)
                logLevelPopup.value = settings.logLevel;
            if (pythonPathField != null)
                pythonPathField.value = settings.pythonServicePath;
        }
        
        private void UpdateSettingsFromUI()
        {
            settings.serviceHost = serviceHostField.value;
            settings.servicePort = servicePortField.value;
            settings.autoStartService = autoStartToggle.value;
            settings.configFilePath = configPathField.value;
            settings.timeoutSeconds = timeoutField.value;
            settings.enableLogging = enableLoggingToggle.value;
            settings.logLevel = logLevelPopup.value;
            settings.pythonServicePath = pythonPathField.value;
            
            // Update computed backend URL
            settings.backendUrl = $"http://{settings.serviceHost}:{settings.servicePort}";
        }
        
        private void ResetToDefaults()
        {
            if (EditorUtility.DisplayDialog("Reset Settings", 
                "Are you sure you want to reset all settings to their default values?", 
                "Reset", "Cancel"))
            {
                settings = new KnowLangSettings();
                BindSettingsToUI();
                connectionStatusLabel.text = "⚠️ Settings reset to defaults";
                connectionStatusLabel.style.color = Color.yellow;
                saveButton.SetEnabled(true);
            }
        }
        
        private void OnSaveClicked()
        {
            SaveSettings();
        }
        
        private void OnResetClicked()
        {
            ResetToDefaults();
        }
    }
    
    [System.Serializable]
    public class KnowLangSettings
    {
        [Header("Service Configuration")]
        public string serviceHost = "127.0.0.1";
        public int servicePort = 8080;
        public bool autoStartService = true;
        public string configFilePath = "";
        
        [Header("Connection Settings")]
        public string backendUrl = "http://127.0.0.1:8080";
        public int timeoutSeconds = 30;
        
        [Header("Service Settings")]
        public string pythonServicePath = "";
        public bool enableLogging = true;
        public string logLevel = "Info";
        
        [Header("Advanced Settings")]
        public int maxRetries = 3;
        public int retryDelayMs = 1000;
        public bool verboseLogging = false;
        public bool saveApiRequests = false;
        
        [Header("Database Settings")]
        public string databasePath = "";
        public bool autoBackup = true;
        public int maxBackups = 5;
        
        // Helper methods
        public string GetServiceHost() => string.IsNullOrEmpty(serviceHost) ? "127.0.0.1" : serviceHost;
        public int GetServicePort() =>  servicePort;
        
        private const string SETTINGS_KEY = "UnityKnowLangSettings";
        private static readonly string SettingsPath = "ProjectSettings/KnowLangSettings.json";
        
        public static KnowLangSettings LoadSettings()
        {
            try
            {
                // Try loading from project settings file first
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonUtility.FromJson<KnowLangSettings>(json);
                    if (settings != null) return settings;
                }
                
                // Fallback to EditorPrefs
                string prefsJson = EditorPrefs.GetString(SETTINGS_KEY, "{}");
                var prefsSettings = JsonUtility.FromJson<KnowLangSettings>(prefsJson);
                return prefsSettings ?? new KnowLangSettings();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load settings: {ex}");
                return new KnowLangSettings();
            }
        }
        
        public static void SaveSettings(KnowLangSettings settings)
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save to project settings file
                string json = JsonUtility.ToJson(settings, true);
                File.WriteAllText(SettingsPath, json);
                
                // Also save to EditorPrefs as backup
                EditorPrefs.SetString(SETTINGS_KEY, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save settings: {ex}");
                throw;
            }
        }
    }
    
    [System.Serializable]
    public class HealthResponse
    {
        public string status;
        public string service;
    }
}
