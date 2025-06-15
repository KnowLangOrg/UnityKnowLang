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
        
        #region Properties
        private ScrollView chatScrollView;
        private TextField messageInput;
        private Button sendButton;
        private Label statusLabel;
        private Button connectionButton;
        private VisualElement chatContainer;
        private KnowLangServerManger serviceManager;
        private ServiceClient serviceClient;
        private ServiceStatusIndicator statusIndicator;
        // Track current streaming message for updates
        private ChatMessage currentStreamingMessage;
        private VisualElement currentStreamingElement;
        #endregion
        
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
        
        #region Hnadle Server Streaming Response 
        private void OnServerMessageReceived(StreamingChatResult result)
        {
            // Update existing streaming message or create new one
            if (currentStreamingMessage != null && currentStreamingElement != null)
            {
                UpdateStreamingMessage(result);
            }
            else
            {
                CreateStreamingMessage(result);
            }
        }

        private void CreateStreamingMessage(StreamingChatResult result)
        {
            currentStreamingMessage = new ChatMessage
            {
                content = result.progress_message,
                isUser = false,
                timestamp = DateTime.Now,
                chatStatus = result.ChatStatus,
                isProgress = true,
                title = GetStatusTitle(result.ChatStatus)
            };
            
            currentStreamingElement = CreateMessageElement(currentStreamingMessage);
            chatContainer.Add(currentStreamingElement);
            
            chatHistory.Add(currentStreamingMessage);
            ScrollToBottom();
        }
        
        // NEW: Update existing streaming message
        private void UpdateStreamingMessage(StreamingChatResult result)
        {
            if (result.ChatStatus == ChatStatus.COMPLETE)
            {
                // Replace with final message
                chatContainer.Remove(currentStreamingElement);
                chatHistory.Remove(currentStreamingMessage);

                var finalMessage = new ChatMessage
                {
                    content = result.answer,
                    isUser = false,
                    timestamp = DateTime.Now,
                    chatStatus = ChatStatus.COMPLETE,
                    codeContexts = ExtractCodeContexts(result.retrieved_context),
                    title = "💻 Code Analysis Result"
                };

                AddChatMessage(finalMessage);

                currentStreamingMessage = null;
                currentStreamingElement = null;
            }
            else
            {
                // Update progress message
                currentStreamingMessage.content = result.progress_message;
                currentStreamingMessage.chatStatus = result.ChatStatus;
                currentStreamingMessage.title = GetStatusTitle(result.ChatStatus);

                // Update UI element
                UpdateMessageElement(currentStreamingElement, currentStreamingMessage);
            }
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

            // Disable send button during processing
            SetSendButtonEnabled(false);

            try
            {
                await serviceClient.StreamChatAsync(message, OnServerMessageReceived);
            }
            catch (Exception ex)
            {
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
        #endregion
        
        #region Chat Interface UI methods
        private void AddChatMessage(ChatMessage message)
        {
            chatHistory.Add(message);
            
            var messageElement = CreateMessageElement(message);
            chatContainer.Add(messageElement);
            
            ScrollToBottom();
        }
        
        private VisualElement CreateMessageElement(ChatMessage message)
        {
            var messageContainer = new VisualElement();
            messageContainer.style.marginBottom = 15;
            messageContainer.style.paddingLeft = 10;
            messageContainer.style.paddingRight = 10;
            
            // Message header with enhanced styling
            CreateMessageHeader(messageContainer, message);
            
            // Message content with code context support
            CreateMessageContent(messageContainer, message);
            
            // Add code contexts if available
            if (message.codeContexts != null && message.codeContexts.Count > 0)
            {
                CreateCodeContextSection(messageContainer, message.codeContexts);
            }
            
            return messageContainer;
        }
        
        // NEW: Create enhanced message header
        private void CreateMessageHeader(VisualElement container, ChatMessage message)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginBottom = 8;
            header.style.alignItems = Align.Center;
            
            // Status icon and sender
            string statusIcon = GetStatusIcon(message);
            string senderText = message.isUser ? "You" : (message.isSystem ? "System" : "Assistant");
            
            var senderLabel = new Label($"{statusIcon} {senderText}");
            senderLabel.style.fontSize = 14;
            
            // Color based on message type
            if (message.isUser)
                senderLabel.style.color = new Color(0.2f, 0.6f, 1f);
            else if (message.isSystem)
                senderLabel.style.color = new Color(0.8f, 0.8f, 0.2f);
            else if (message.isProgress)
                senderLabel.style.color = new Color(1f, 0.6f, 0.2f);
            else
                senderLabel.style.color = new Color(0.2f, 0.8f, 0.2f);
                
            header.Add(senderLabel);
            
            // Title if available
            if (!string.IsNullOrEmpty(message.title))
            {
                var titleLabel = new Label($"• {message.title}");
                titleLabel.style.fontSize = 12;
                titleLabel.style.color = Color.gray;
                titleLabel.style.marginLeft = 10;
                header.Add(titleLabel);
            }
            
            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            
            // Timestamp
            var timestampLabel = new Label(message.timestamp.ToString("HH:mm:ss"));
            timestampLabel.style.fontSize = 10;
            timestampLabel.style.color = Color.gray;
            header.Add(timestampLabel);
            
            container.Add(header);
        }
        
        // NEW: Create enhanced message content
        private void CreateMessageContent(VisualElement container, ChatMessage message)
        {
            var contentContainer = new VisualElement();
            contentContainer.style.paddingLeft = 15;
            contentContainer.style.paddingRight = 10;
            contentContainer.style.paddingTop = 8;
            contentContainer.style.paddingBottom = 8;
            
            // Set background colors based on message type
            if (message.isUser)
                contentContainer.style.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.15f);
            else if (message.isSystem)
                contentContainer.style.backgroundColor = new Color(0.8f, 0.8f, 0.2f, 0.15f);
            else if (message.isProgress)
                contentContainer.style.backgroundColor = new Color(1f, 0.6f, 0.2f, 0.15f);
            else
                contentContainer.style.backgroundColor = new Color(0.2f, 0.8f, 0.2f, 0.15f);
            
            if (message.isError)
            {
                contentContainer.style.backgroundColor = new Color(1f, 0.2f, 0.2f, 0.15f);
                contentContainer.style.borderLeftWidth = 4;
                contentContainer.style.borderLeftColor = Color.red;
            }
            
            // Message content
            var contentLabel = new Label(message.content);
            contentLabel.style.whiteSpace = WhiteSpace.Normal;
            contentLabel.style.fontSize = 13;
            
            if (message.isError)
                contentLabel.style.color = new Color(0.8f, 0.2f, 0.2f);
            else if (message.isProgress)
                contentLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            
            contentContainer.Add(contentLabel);
            container.Add(contentContainer);
        }
        
        // NEW: Create collapsible code context section
        private void CreateCodeContextSection(VisualElement container, List<CodeContext> codeContexts)
        {
            var codeSection = new VisualElement();
            codeSection.style.marginTop = 10;
            codeSection.style.paddingLeft = 15;
            
            // Section header
            var headerContainer = new VisualElement();
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginBottom = 8;
            
            var foldout = new Foldout();
            foldout.text = $"📄 Code Context ({codeContexts.Count} files)";
            foldout.value = true; // Expanded by default
            foldout.style.fontSize = 12;
            
            // Create code blocks inside foldout
            var codeContainer = new VisualElement();
            
            foreach (var context in codeContexts)
            {
                var codeBlock = CreateCodeBlock(context);
                codeContainer.Add(codeBlock);
            }
            
            foldout.Add(codeContainer);
            codeSection.Add(foldout);
            container.Add(codeSection);
        }
        
        // NEW: Create individual code block with syntax highlighting
        private VisualElement CreateCodeBlock(CodeContext context)
        {
            var blockContainer = new VisualElement();
            blockContainer.style.marginBottom = 10;
            blockContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            // File header
            var fileHeader = new Label(context.GetTitle());
            fileHeader.style.fontSize = 11;
            fileHeader.style.color = new Color(0.7f, 0.9f, 1f);
            fileHeader.style.marginBottom = 5;
            blockContainer.Add(fileHeader);
            
            // Code content
            var codeLabel = new Label(context.code);
            codeLabel.style.whiteSpace = WhiteSpace.Normal;
            codeLabel.style.fontSize = 11;
            codeLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            blockContainer.Add(codeLabel);
            
            return blockContainer;
        }
        
        // NEW: Update existing message element
        private void UpdateMessageElement(VisualElement element, ChatMessage message)
        {
            // Find and update content label
            var labels = element.Query<Label>().ToList();
            if (labels.Count > 2) // Skip header labels
            {
                labels[2].text = message.content; // Content label is typically the 3rd label
            }
        }
        
        // NEW: Helper methods for status handling
        private string GetStatusIcon(ChatMessage message)
        {
            if (message.isUser) return "👤";
            if (message.isSystem) return "⚙️";
            if (message.isError) return "❌";
            
            return message.chatStatus switch
            {
                ChatStatus.STARTING => "🔄",
                ChatStatus.RETRIEVING => "🔍",
                ChatStatus.ANSWERING => "💭",
                ChatStatus.COMPLETE => "✅",
                ChatStatus.ERROR => "❌",
                _ => "🤖"
            };
        }
        
        private string GetStatusTitle(ChatStatus status)
        {
            return status switch
            {
                ChatStatus.STARTING => "Starting Analysis",
                ChatStatus.RETRIEVING => "Searching Codebase",
                ChatStatus.ANSWERING => "Generating Response",
                ChatStatus.COMPLETE => "Analysis Complete",
                ChatStatus.ERROR => "Error Occurred",
                _ => "Processing"
            };
        }

        private List<CodeContext> ExtractCodeContexts(List<SearchResult> searchResults)
        {
            if (searchResults == null) return new List<CodeContext>();
            
            var contexts = new List<CodeContext>();
            foreach (var result in searchResults)
            {
                contexts.Add(new CodeContext
                {
                    filePath = result.metadata.ContainsKey("file_path") ? result.metadata["file_path"].ToString() : "Unknown",
                    startLine = result.metadata.ContainsKey("start_line") ? Convert.ToInt32(result.metadata["start_line"]) : 0,
                    endLine = result.metadata.ContainsKey("end_line") ? Convert.ToInt32(result.metadata["end_line"]) : 0,
                    code = result.document
                });
            }
            return contexts;
        }
        
        private void ScrollToBottom()
        {
            EditorApplication.delayCall += () =>
            {
                chatScrollView.scrollOffset = new Vector2(0, chatScrollView.contentContainer.layout.height);
            };
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
                    // UpdateConnectionStatus(false, "Disconnected");
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
            
            SetSendButtonEnabled(connected);
                
            // Update connection button text
            if (connectionButton != null)
                connectionButton.text = connected ? "Disconnect" : "Connect";
        }
        
        private void SetSendButtonEnabled(bool enabled)
        {
            if (sendButton != null)
                sendButton.SetEnabled(enabled && serviceManager?.IsRunning == true);
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

        public bool isProgress = false;
        public string title;
        public ChatStatus chatStatus = ChatStatus.COMPLETE;
        public List<CodeContext> codeContexts;
        public bool isCollapsible = false;
    }
    
}