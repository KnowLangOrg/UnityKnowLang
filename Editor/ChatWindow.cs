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
        
        private ScrollView chatScrollView;
        private TextField messageInput;
        private Button sendButton;
        private Label statusLabel;
        private VisualElement chatContainer;
        private PythonServiceManager serviceManager;
        
        [MenuItem("Window/UnityKnowLang/Chat Interface")]
        public static void ShowWindow()
        {
            ChatWindow window = GetWindow<ChatWindow>();
            window.titleContent = new GUIContent("Python Chat Interface");
            window.minSize = new Vector2(400, 300);
            window.maxSize = new Vector2(1200, 800);
        }

        public void CreateGUI()
        {
            // Initialize service manager
            serviceManager = new PythonServiceManager();
            
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
            
            // Initialize service connection
            InitializeService();
            
            // Restore chat history
            RestoreChatHistory();
        }
        
        private void CreateHeader(VisualElement parent)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.paddingBottom = 10;
            header.style.paddingTop = 10;
            header.style.paddingLeft = 10;
            header.style.paddingRight = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Color.gray;
            
            // Service status
            statusLabel = new Label($"Status: {serviceStatus}");
            statusLabel.style.flexGrow = 1;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            header.Add(statusLabel);
            
            // Settings button
            var settingsButton = new Button(() => SettingsWindow.ShowWindow())
            {
                text = "Settings"
            };
            settingsButton.style.width = 80;
            header.Add(settingsButton);
            
            // Connect/Disconnect button
            var connectionButton = new Button(ToggleConnection)
            {
                text = serviceConnected ? "Disconnect" : "Connect"
            };
            connectionButton.style.width = 100;
            connectionButton.style.marginLeft = 5;
            header.Add(connectionButton);
            
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
            
            // Message input field
            messageInput = new TextField();
            messageInput.multiline = true;
            messageInput.style.flexGrow = 1;
            messageInput.style.minHeight = 40;
            messageInput.style.marginRight = 10;
            
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
            sendButton.SetEnabled(serviceConnected);
            
            inputContainer.Add(sendButton);
            
            parent.Add(inputContainer);
        }
        
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
            if (string.IsNullOrEmpty(message) || !serviceConnected)
                return;
                
            // Clear input
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
            
            try
            {
                // Send to Python service
                var response = await serviceManager.SendChatMessageAsync(message);
                
                // Remove thinking indicator
                RemoveThinkingMessage();
                
                // Add response
                AddChatMessage(new ChatMessage
                {
                    content = response,
                    isUser = false,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                // Remove thinking indicator
                RemoveThinkingMessage();
                
                // Add error message
                AddChatMessage(new ChatMessage
                {
                    content = $"Error: {ex.Message}",
                    isUser = false,
                    timestamp = DateTime.Now,
                    isError = true
                });
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
            
            var senderLabel = new Label(message.isUser ? "You" : "Assistant");
            senderLabel.style.color = message.isUser ? new Color(0.2f, 0.6f, 1f) : new Color(0.6f, 0.6f, 0.6f);
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
            contentLabel.style.backgroundColor = message.isUser ? 
                new Color(0.2f, 0.3f, 0.4f, 0.3f) : 
                new Color(0.1f, 0.1f, 0.1f, 0.1f);
            
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
        
        private async void InitializeService()
        {
            try
            {
                await serviceManager.InitializeAsync();
                UpdateConnectionStatus(true, "Connected");
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false, $"Connection failed: {ex.Message}");
            }
        }
        
        private async void ToggleConnection()
        {
            try
            {
                if (serviceConnected)
                {
                    await serviceManager.DisconnectAsync();
                    UpdateConnectionStatus(false, "Disconnected");
                }
                else
                {
                    await serviceManager.ConnectAsync();
                    UpdateConnectionStatus(true, "Connected");
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
            
            if (statusLabel != null)
                statusLabel.text = $"Status: {serviceStatus}";
                
            if (sendButton != null)
                sendButton.SetEnabled(connected);
                
            // Update connection button text
            var connectionButton = rootVisualElement.Q<Button>("connectionButton");
            if (connectionButton != null)
                connectionButton.text = connected ? "Disconnect" : "Connect";
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
        
        void OnDisable()
        {
            // Clean up when window is closed
            serviceManager?.Dispose();
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
    }
    
    // Placeholder service manager - you'll implement the actual HTTP communication
    public class PythonServiceManager : IDisposable
    {
        private bool isConnected = false;
        
        public async Task InitializeAsync()
        {
            // TODO: Initialize connection to Python service
            await Task.Delay(1000); // Simulate connection time
            isConnected = true;
        }
        
        public async Task ConnectAsync()
        {
            // TODO: Connect to Python service
            await Task.Delay(500);
            isConnected = true;
        }
        
        public async Task DisconnectAsync()
        {
            // TODO: Disconnect from Python service
            await Task.Delay(200);
            isConnected = false;
        }
        
        public async Task<string> SendChatMessageAsync(string message)
        {
            if (!isConnected)
                throw new Exception("Service not connected");
                
            // TODO: Send HTTP request to Python FastAPI service
            await Task.Delay(1000); // Simulate processing time
            
            // Placeholder response
            return $"Python service received: '{message}'. This is a placeholder response that you'll replace with actual API communication.";
        }
        
        public void Dispose()
        {
            // TODO: Clean up resources
            isConnected = false;
        }
    }
}