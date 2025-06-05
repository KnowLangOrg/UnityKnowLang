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
        
        private TextField backendUrlField;
        private IntegerField timeoutField;
        private Toggle enableLoggingToggle;
        private PopupField<string> logLevelPopup;
        private TextField pythonPathField;
        private Button browseButton;
        private Button testConnectionButton;
        private Button saveButton;
        private Button resetButton;
        private Label connectionStatusLabel;
        private VisualElement advancedSection;
        private Toggle showAdvancedToggle;
        
        private static readonly string SettingsPath = "ProjectSettings/KnowLangSettings.json";
        
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
            
            // Create main container
            var mainContainer = new VisualElement();
            rootVisualElement.Add(mainContainer);
            
            // Create title
            CreateTitle(mainContainer);
            
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
        
        private void CreateConnectionSection(VisualElement parent)
        {
            var section = CreateSection("Connection Settings", parent);
            
            // Backend URL
            var urlContainer = CreateFieldContainer("Backend URL:", section);
            backendUrlField = new TextField();
            backendUrlField.style.flexGrow = 1;
            urlContainer.Add(backendUrlField);
            
            // Timeout
            var timeoutContainer = CreateFieldContainer("Timeout (seconds):", section);
            timeoutField = new IntegerField();
            timeoutField.style.width = 100;
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
            loggingContainer.Add(enableLoggingToggle);
            
            // Log level
            var logLevelContainer = CreateFieldContainer("Log Level:", section);
            logLevelPopup = new PopupField<string>( new List<string> { "Debug", "Info", "Warning", "Error" }, "Info");
            logLevelPopup.style.width = 150;
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
        }
        
        private void CreateActionButtons(VisualElement parent)
        {
            var buttonContainer = new VisualElement();
            buttonContainer.style.flexDirection = FlexDirection.Row;
            buttonContainer.style.justifyContent = Justify.SpaceBetween;
            buttonContainer.style.marginTop = 30;
            
            // Reset to defaults
            resetButton = new Button(ResetToDefaults)
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
            
            saveButton = new Button(SaveSettings)
            {
                text = "Save"
            };
            saveButton.style.width = 80;
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
        
        private void OnAdvancedToggleChanged(ChangeEvent<bool> evt)
        {
            advancedSection.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private async void TestConnection()
        {
            testConnectionButton.SetEnabled(false);
            testConnectionButton.text = "Testing...";
            connectionStatusLabel.text = "Testing connection...";
            connectionStatusLabel.style.color = Color.yellow;
            
            try
            {
                // TODO: Implement actual connection test
                await Task.Delay(2000); // Simulate testing
                
                // Placeholder result
                bool connectionSuccess = !string.IsNullOrEmpty(backendUrlField.value) && 
                                       backendUrlField.value.StartsWith("http");
                
                if (connectionSuccess)
                {
                    connectionStatusLabel.text = "✓ Connection successful";
                    connectionStatusLabel.style.color = Color.green;
                }
                else
                {
                    connectionStatusLabel.text = "✗ Connection failed: Invalid URL";
                    connectionStatusLabel.style.color = Color.red;
                }
            }
            catch (Exception ex)
            {
                connectionStatusLabel.text = $"✗ Connection failed: {ex.Message}";
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
            string path = EditorUtility.OpenFilePanel("Select Python Service Executable", 
                                                     Application.streamingAssetsPath, "exe");
            if (!string.IsNullOrEmpty(path))
            {
                pythonPathField.value = path;
            }
        }
        
        private void SaveSettings()
        {
            // Update settings from UI
            UpdateSettingsFromUI();
            
            // Save to file
            try
            {
                string json = JsonUtility.ToJson(settings, true);
                File.WriteAllText(SettingsPath, json);
                
                connectionStatusLabel.text = "✓ Settings saved successfully";
                connectionStatusLabel.style.color = Color.green;
                
                Debug.Log("UnityKnowLang settings saved");
            }
            catch (Exception ex)
            {
                connectionStatusLabel.text = $"✗ Failed to save settings: {ex.Message}";
                connectionStatusLabel.style.color = Color.red;
                Debug.LogError($"Failed to save settings: {ex}");
            }
        }
        
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    settings = JsonUtility.FromJson<KnowLangSettings>(json);
                }
                else
                {
                    // Use default settings
                    settings = new KnowLangSettings();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load settings: {ex}");
                settings = new KnowLangSettings();
            }
        }
        
        private void BindSettingsToUI()
        {
            if (backendUrlField != null)
                backendUrlField.value = settings.backendUrl;
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
            settings.backendUrl = backendUrlField.value;
            settings.timeoutSeconds = timeoutField.value;
            settings.enableLogging = enableLoggingToggle.value;
            settings.logLevel = logLevelPopup.value;
            settings.pythonServicePath = pythonPathField.value;
        }
        
        private void ResetToDefaults()
        {
            settings = new KnowLangSettings();
            BindSettingsToUI();
            connectionStatusLabel.text = "Settings reset to defaults";
            connectionStatusLabel.style.color = Color.yellow;
        }
    }
    
    [System.Serializable]
    public class KnowLangSettings
    {
        [Header("Connection Settings")]
        public string backendUrl = "http://localhost:8000";
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
    }
}