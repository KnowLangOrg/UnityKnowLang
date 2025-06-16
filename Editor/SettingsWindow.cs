using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityKnowLang.Editor
{
    public class SettingsWindow : EditorWindow
    {
        #region Fields
        [SerializeField] private KnowLangSettings settings = new KnowLangSettings();
        
        private TextField serviceHostField;
        private IntegerField servicePortField;
        private Toggle autoStartToggle;
        private Button saveButton;
        private Button resetButton;
        #endregion

        #region Unity Menu and Window Management
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
            // Create main container with scroll view
            var scrollView = new ScrollView();
            rootVisualElement.Add(scrollView);
            
            var mainContainer = new VisualElement();
            scrollView.Add(mainContainer);
            
            // Create title
            CreateTitle(mainContainer);
            
            // Create service configuration section
            CreateServiceConfigSection(mainContainer);
            
            // Create action buttons
            CreateActionButtons(mainContainer);
            
            // Bind values to UI
            BindSettingsToUI();
        }
        #endregion

        #region UI Creation Methods
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
        #endregion

        #region UI Helper Methods
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
        #endregion

        #region Platform Utility Methods
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
        #endregion

        #region Settings Data Binding
        private void BindSettingsToUI()
        {
            if (serviceHostField != null)
                serviceHostField.value = settings.serviceHost;
            if (servicePortField != null)
                servicePortField.value = settings.servicePort;
            if (autoStartToggle != null)
                autoStartToggle.value = settings.autoStartService;
        }
        
        private void UpdateSettingsFromUI()
        {
            settings.serviceHost = serviceHostField.value;
            settings.servicePort = servicePortField.value;
            settings.autoStartService = autoStartToggle.value;
        }
        #endregion

        #region Settings Management
        private void SaveSettings()
        {
            // Update settings from UI
            UpdateSettingsFromUI();
            
            // Save using the static method
            try
            {
                KnowLangSettings.SaveSettings(settings);
                
                // Disable save button until next change
                saveButton.SetEnabled(false);
                
                Debug.Log("UnityKnowLang settings saved");
                
                // Show popup to inform user to restart service
                EditorUtility.DisplayDialog("Settings Saved", 
                    "Settings have been saved successfully.\n\nPlease restart the KnowLang service for changes to take effect.", 
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save settings: {ex}");
                EditorUtility.DisplayDialog("Save Failed", 
                    $"Failed to save settings:\n{ex.Message}", 
                    "OK");
            }
        }
        
        private void LoadSettings()
        {
            settings = KnowLangSettings.LoadSettings();
        }
        
        private void ResetToDefaults()
        {
            if (EditorUtility.DisplayDialog("Reset Settings", 
                "Are you sure you want to reset all settings to their default values?", 
                "Reset", "Cancel"))
            {
                settings = new KnowLangSettings();
                BindSettingsToUI();
                saveButton.SetEnabled(true);
            }
        }
        #endregion

        #region Event Handlers
        private void OnSettingChanged<T>(ChangeEvent<T> evt)
        {
            // Enable save button when settings change
            if (saveButton != null)
                saveButton.SetEnabled(true);
        }

        private void OnSaveClicked()
        {
            SaveSettings();
        }
        
        private void OnResetClicked()
        {
            ResetToDefaults();
        }
        #endregion
    }
    
    [System.Serializable]
    public class KnowLangSettings
    {
        #region Settings Properties
        [Header("Service Configuration")]
        public string serviceHost = "127.0.0.1";
        public int servicePort = 8080;
        public bool autoStartService = true;
        #endregion

        #region Constants and Static Fields
        private const string SETTINGS_KEY = "UnityKnowLangSettings";
        private static readonly string SettingsPath = "ProjectSettings/KnowLangSettings.json";
        #endregion

        #region Public API Methods
        public string GetServiceHost() => string.IsNullOrEmpty(serviceHost) ? "127.0.0.1" : serviceHost;
        public int GetServicePort() =>  servicePort;
        #endregion

        #region Static Settings Management
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
        #endregion
    }
}
