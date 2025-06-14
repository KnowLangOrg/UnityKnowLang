using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Threading.Tasks;

namespace UnityKnowLang.Editor
{
    public class ChatWindow : EditorWindow
    {
        [SerializeField] private List<ChatMessage> chatHistory = new List<ChatMessage>();
        [SerializeField] private bool serviceConnected = false;
        [SerializeField] private string serviceStatus = "Disconnected";
        [SerializeField] private string currentQuery = "";
        
        private ScrollView chatScrollView;
        private TextField messageInput;
        private Button sendButton;
        private Label statusLabel;
        private Button connectionButton;
        private VisualElement chatContainer;
        private KnowLangServerManger serviceManager;
        private ServiceClient serviceClient;
        private ServiceStatusIndicator statusIndicator;
        
        [MenuItem("Window/UnityKnowLang/Chat Interface")]
        public static void ShowWindow()
        {
            ChatWindow window = GetWindow<ChatWindow>();
            window.titleContent = new GUIContent("Python Chat Interface");
            window.minSize = new Vector2(400, 300);
            window.maxSize = new Vector2(1200, 800);
        }

        #region UI Components Creation
        public void CreateGUI()
        {
            // Initialize service manager if not already done
            if (serviceManager == null)
            {
                InitializeServiceManager();
            }
            
            // Create main container
            var mainContainer = new VisualElement();
            mainContainer.style.flexGrow = 1;
            rootVisualElement.Add(mainContainer);
            
            // Create header with status and controls
            CreateHeader(mainContainer);
            
            // Create chat area
            CreateChatArea(mainContainer);
            
            // Create input area
            CreateInputArea(mainContainer);
            
            // Restore chat history
            RestoreChatHistory();
            
            // Auto-start service if configured
            var settings = KnowLangSettings.LoadSettings();
            if (settings.autoStartService && !serviceManager.IsRunning)
            {
                _ = StartServiceAsync();
            }
        }

        private void CreateHeader(VisualElement parent)
        {
            void HeaderStyling(VisualElement header)
            {
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.Center;
                header.style.height = 60;
                header.style.paddingBottom = 10;
                header.style.paddingTop = 10;
                header.style.paddingLeft = 10;
                header.style.paddingRight = 10;
                header.style.borderBottomWidth = 1;
                header.style.borderBottomColor = Color.gray;
            }
            void CreateStatusIndicator(VisualElement header)
            {
                statusIndicator = new ServiceStatusIndicator();
                statusIndicator.SetServiceManager(serviceManager);
                statusIndicator.style.flexGrow = 1;
                header.Add(statusIndicator);
            }
            void CreateSettingsButton(VisualElement header)
            {
                var settingsButton = new Button(() => SettingsWindow.ShowWindow())
                {
                    text = "Settings"
                };
                settingsButton.style.width = 100;
                settingsButton.style.height = 30;
                header.Add(settingsButton);
            }
            void CreateConnectionButton(VisualElement header)
            {
                connectionButton = new Button(ToggleConnection)
                {
                    text = serviceConnected ? "Disconnect" : "Connect"
                };
                connectionButton.style.height = 30;
                connectionButton.style.width = 100;
                connectionButton.style.marginLeft = 5;
                header.Add(connectionButton);
            }

            void CreateClearChatButton(VisualElement header)
            {
                var clearChatButton = new Button(() => {
                    chatHistory.Clear();
                    chatContainer.Clear();
                })
                {
                    text = "Clear History"
                };
                clearChatButton.style.height = 30;
                clearChatButton.style.width = 100;
                clearChatButton.style.marginLeft = 5;
                header.Add(clearChatButton);
            }

            var header = new VisualElement();
            HeaderStyling(header);
            CreateStatusIndicator(header);
            CreateSettingsButton(header);
            CreateConnectionButton(header);
            CreateClearChatButton(header);

            parent.Add(header);
        }
        
        private void CreateChatArea(VisualElement parent)
        {
            // Chat container with scroll view
            chatScrollView = new ScrollView(ScrollViewMode.Vertical);
            chatScrollView.style.flexGrow = 1;
            chatScrollView.style.marginLeft = 10;
            chatScrollView.style.marginRight = 10;
            chatScrollView.style.marginTop = 10;
            chatScrollView.style.borderBottomWidth = 1;
            chatScrollView.style.borderBottomColor = Color.gray;
            
            chatContainer = new VisualElement();
            chatContainer.style.flexGrow = 1;
            chatScrollView.Add(chatContainer);
            
            parent.Add(chatScrollView);
        }
        
        private void CreateInputArea(VisualElement parent)
        {
            var inputContainer = new VisualElement();
            inputContainer.style.flexDirection = FlexDirection.Row;
            inputContainer.style.minHeight = 60;
            inputContainer.style.marginTop = 10;
            inputContainer.style.paddingLeft = 10;
            inputContainer.style.paddingRight = 10;
            
            // Message input field
            messageInput = new TextField();
            messageInput.multiline = true;
            messageInput.style.flexGrow = 1;
            messageInput.style.minHeight = 40;
            messageInput.style.marginRight = 10;
            messageInput.value = currentQuery;
            messageInput.RegisterValueChangedCallback(evt => currentQuery = evt.newValue);
            
            // Handle Enter key
            messageInput.RegisterCallback<KeyDownEvent>(OnMessageInputKeyDown);
            
            inputContainer.Add(messageInput);
            
            // Send button
            sendButton = new Button(SendMessage)
            {
                text = "Send"
            };
            sendButton.style.width = 80;
            sendButton.style.minHeight = 40;
            sendButton.SetEnabled(serviceConnected && !string.IsNullOrWhiteSpace(currentQuery));
            
            inputContainer.Add(sendButton);
            
            parent.Add(inputContainer);
        }
        #endregion
        
        #region UI Event Handlers
        private void OnMessageInputKeyDown(KeyDownEvent evt)
        {
            // Send message on Ctrl+Enter or Cmd+Enter
            if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.Return)
            {
                SendMessage();
                evt.StopPropagation();
            }
        }
        
        private async void SendMessage()
        {
            string message = messageInput.text.Trim();
            if (string.IsNullOrEmpty(message) || !serviceManager.IsRunning)
                return;
                
            // Clear input
            currentQuery = "";
            messageInput.value = "";
            
            // Add user message to chat
            AddChatMessage(new ChatMessage
            {
                content = message,
                isUser = true,
                timestamp = DateTime.Now
            });
            
            // Show thinking indicator
            var thinkingMessage = new ChatMessage
            {
                content = "Thinking...",
                isUser = false,
                timestamp = DateTime.Now,
                isThinking = true
            };
            AddChatMessage(thinkingMessage);
            
            // Disable send button during processing
            SetSendButtonEnabled(false);
            
            try
            {
                // Send streaming request to Python service
                var requestData = new ChatRequest { query = message };
                
                string assistantResponse = "";
                await serviceClient.PostStreamAsync("api/v1/chat/stream", requestData, (chunk) => {
                    // Process streaming chunks
                    assistantResponse += chunk;
                });
                
                // Remove thinking indicator
                RemoveThinkingMessage();
                
                // Add response
                if (!string.IsNullOrEmpty(assistantResponse))
                {
                    AddChatMessage(new ChatMessage
                    {
                        content = assistantResponse,
                        isUser = false,
                        timestamp = DateTime.Now
                    });
                }
                else
                {
                    AddChatMessage(new ChatMessage
                    {
                        content = "⚠️ No response received from the service.",
                        isUser = false,
                        timestamp = DateTime.Now,
                        isError = true
                    });
                }
            }
            catch (Exception ex)
            {
                // Remove thinking indicator
                RemoveThinkingMessage();
                
                // Add error message
                AddChatMessage(new ChatMessage
                {
                    content = $"❌ Error: {ex.Message}",
                    isUser = false,
                    timestamp = DateTime.Now,
                    isError = true
                });
                
                Debug.LogError($"Chat error: {ex}");
            }
            finally
            {
                SetSendButtonEnabled(true);
            }
        }
        
        private void AddChatMessage(ChatMessage message)
        {
            chatHistory.Add(message);
            
            var messageElement = CreateMessageElement(message);
            chatContainer.Add(messageElement);
            
            // Scroll to bottom
            EditorApplication.delayCall += () =>
            {
                chatScrollView.scrollOffset = new Vector2(0, chatScrollView.contentContainer.layout.height);
            };
        }
        
        private void RemoveThinkingMessage()
        {
            // Remove the last thinking message from history and UI
            for (int i = chatHistory.Count - 1; i >= 0; i--)
            {
                if (chatHistory[i].isThinking)
                {
                    chatHistory.RemoveAt(i);
                    chatContainer.RemoveAt(chatContainer.childCount - 1);
                    break;
                }
            }
        }
        
        private VisualElement CreateMessageElement(ChatMessage message)
        {
            var messageContainer = new VisualElement();
            messageContainer.style.marginBottom = 10;
            messageContainer.style.paddingLeft = 10;
            messageContainer.style.paddingRight = 10;
            
            // Message header with timestamp
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginBottom = 5;
            
            string senderText = message.isUser ? "You" : (message.isSystem ? "System" : "Assistant");
            var senderLabel = new Label(senderText);
            
            if (message.isUser)
                senderLabel.style.color = new Color(0.2f, 0.6f, 1f);
            else if (message.isSystem)
                senderLabel.style.color = new Color(0.8f, 0.8f, 0.2f);
            else
                senderLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                
            header.Add(senderLabel);
            
            var timestampLabel = new Label(message.timestamp.ToString("HH:mm:ss"));
            timestampLabel.style.fontSize = 10;
            timestampLabel.style.color = Color.gray;
            timestampLabel.style.marginLeft = 10;
            header.Add(timestampLabel);
            
            messageContainer.Add(header);
            
            // Message content
            var contentLabel = new Label(message.content);
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            contentLabel.style.paddingLeft = 10;
            contentLabel.style.paddingRight = 10;
            contentLabel.style.paddingTop = 5;
            contentLabel.style.paddingBottom = 5;
            
            // Set background colors based on message type
            if (message.isUser)
                contentLabel.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.1f);
            else if (message.isSystem)
                contentLabel.style.backgroundColor = new Color(0.8f, 0.8f, 0.2f, 0.1f);
            else
                contentLabel.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.1f);
            
            if (message.isError)
            {
                contentLabel.style.color = Color.red;
                contentLabel.style.backgroundColor = new Color(1f, 0.2f, 0.2f, 0.1f);
            }
            else if (message.isThinking)
            {
                contentLabel.style.color = Color.gray;
            }
            
            messageContainer.Add(contentLabel);
            
            return messageContainer;
        }
        #endregion
        
        private void InitializeServiceManager()
        {
            var settings = KnowLangSettings.LoadSettings();
            var config = new ServiceConfig
            {
                Host = settings.GetServiceHost(),
                Port = settings.GetServicePort(),
                AutoStart = settings.autoStartService
            };

            serviceManager = new KnowLangServerManger(config);
            serviceClient = new ServiceClient(serviceManager.ServiceUrl);

            // Subscribe to service events
            serviceManager.OnStatusChanged += OnServiceStatusChanged;
            serviceManager.OnServiceLog += OnServiceLog;
            serviceManager.OnServiceError += OnServiceError;
        }
        
        private async Task StartServiceAsync()
        {
            try
            {
                bool success = await serviceManager.StartServiceAsync();
                if (success)
                {
                    AddSystemMessage("✅ KnowLang service started successfully!");
                }
                else
                {
                    AddSystemMessage("❌ Failed to start KnowLang service. Please check the console for details.");
                }
            }
            catch (Exception ex)
            {
                AddSystemMessage($"❌ Service startup error: {ex.Message}");
            }
        }
        
        private async void ToggleConnection()
        {
            try
            {
                if (serviceManager.IsRunning)
                {
                    serviceManager.StopService();
                    UpdateConnectionStatus(false, "Disconnected");
                }
                else
                {
                    await StartServiceAsync();
                }
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false, $"Connection error: {ex.Message}");
            }
        }
        
        private void UpdateConnectionStatus(bool connected, string status)
        {
            serviceConnected = connected;
            serviceStatus = status;
            
            SetSendButtonEnabled(connected && !string.IsNullOrWhiteSpace(currentQuery));
                
            // Update connection button text
            if (connectionButton != null)
                connectionButton.text = connected ? "Disconnect" : "Connect";
        }
        
        private void SetSendButtonEnabled(bool enabled)
        {
            if (sendButton != null)
                sendButton.SetEnabled(enabled && serviceManager?.IsRunning == true && !string.IsNullOrWhiteSpace(currentQuery));
        }
        
        private void AddSystemMessage(string text)
        {
            var message = new ChatMessage
            {
                content = text,
                isUser = false,
                timestamp = DateTime.Now,
                isSystem = true
            };
            AddChatMessage(message);
        }
        
        private void OnServiceStatusChanged(ServiceStatus status)
        {
            bool connected = status == ServiceStatus.Running;
            UpdateConnectionStatus(connected, status.ToString());
            
            // Add status messages to chat
            switch (status)
            {
                case ServiceStatus.Running:
                    AddSystemMessage("🟢 Service connected and ready");
                    break;
                case ServiceStatus.Stopped:
                    AddSystemMessage("🔴 Service disconnected");
                    break;
                case ServiceStatus.Error:
                    AddSystemMessage("🔴 Service error - check console for details");
                    break;
            }
        }
        
        private void OnServiceLog(string message)
        {
            Debug.Log($"[KnowLang Service] {message}");
        }
        
        private void OnServiceError(string error)
        {
            Debug.LogError($"[KnowLang Service] {error}");
        }
        
        private void RestoreChatHistory()
        {
            foreach (var message in chatHistory)
            {
                var messageElement = CreateMessageElement(message);
                chatContainer.Add(messageElement);
            }
            
            // Scroll to bottom
            EditorApplication.delayCall += () =>
            {
                chatScrollView.scrollOffset = new Vector2(0, chatScrollView.contentContainer.layout.height);
            };
        }
        
        void OnDestroy()
        {
            // Clean up when window is closed
            serviceManager?.Dispose();
        }
        
        void OnEnable()
        {
            // Restore state after assembly reload
            if (serviceManager == null)
            {
                InitializeServiceManager();
            }
        }
    }
    
    [System.Serializable]
    public class ChatMessage
    {
        public string content;
        public bool isUser;
        public DateTime timestamp;
        public bool isThinking = false;
        public bool isError = false;
        public bool isSystem = false;
    }
    
    [System.Serializable]
    public class ChatRequest
    {
        public string query;
    }
}