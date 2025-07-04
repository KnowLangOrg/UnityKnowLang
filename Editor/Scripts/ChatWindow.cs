using System;
using System.IO;
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
                header.style.minHeight = 50;
                header.style.paddingBottom = 8;
                header.style.paddingTop = 8;
                header.style.paddingLeft = 10;
                header.style.paddingRight = 10;
                header.style.borderBottomWidth = 1;
                header.style.borderBottomColor = Color.gray;
                header.style.flexWrap = Wrap.Wrap; // Allow wrapping on small screens
            }
            void CreateStatusIndicator(VisualElement header)
            {
                statusIndicator = new ServiceStatusIndicator();
                statusIndicator.SetServiceManager(serviceManager);
                statusIndicator.style.flexGrow = 1;
                statusIndicator.style.minWidth = 200; // Ensure enough space for status + button
                statusIndicator.style.marginRight = 8; // Space before other buttons
                header.Add(statusIndicator);
            }
            void CreateSettingsButton(VisualElement header)
            {
                var settingsButton = new Button(() => SettingsWindow.ShowWindow())
                {
                    text = "Settings"
                };
                // Responsive button sizing
                settingsButton.style.minWidth = 60;
                settingsButton.style.maxWidth = 100;
                settingsButton.style.height = 28;
                settingsButton.style.flexShrink = 1;
                settingsButton.style.marginLeft = 3;
                header.Add(settingsButton);
            }

            void CreateParseButton(VisualElement header)
            {
                var parseButton = new Button(ParseProject)
                {
                    text = "Parse Project"
                };
                // Responsive button sizing
                parseButton.style.minWidth = 70;
                parseButton.style.maxWidth = 120;
                parseButton.style.height = 28;
                parseButton.style.flexShrink = 1;
                parseButton.style.marginLeft = 3;
                header.Add(parseButton);
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
                // Responsive button sizing
                clearChatButton.style.minWidth = 70;
                clearChatButton.style.maxWidth = 120;
                clearChatButton.style.height = 28;
                clearChatButton.style.flexShrink = 1;
                clearChatButton.style.marginLeft = 3;
                header.Add(clearChatButton);
            }

            var header = new VisualElement();
            HeaderStyling(header);
            CreateStatusIndicator(header);
            CreateSettingsButton(header);
            CreateParseButton(header);
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
                messageInput.style.alignSelf = Align.Stretch; // Height!
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
                sendButton.style.minHeight = 20;
                sendButton.style.alignSelf = Align.Stretch; // Height!
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
                    chatStatus = result.status,
                    isProgress = true,
                    title = GetStatusTitle(result.status)
                };
                
                currentStreamingElement = CreateMessageElement(currentStreamingMessage);
                chatContainer.Add(currentStreamingElement);
                
                chatHistory.Add(currentStreamingMessage);
                ScrollToBottom();
            }
            
            // NEW: Update existing streaming message
            private void UpdateStreamingMessage(StreamingChatResult result)
            {
                if (result.status == ChatStatus.COMPLETE)
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
                    currentStreamingMessage.chatStatus = result.status;
                    currentStreamingMessage.title = GetStatusTitle(result.status);

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
            
            private async void ParseProject()
            {
                if (!serviceManager.IsRunning)
                {
                    AddSystemMessage("❌ Service is not running. Please connect first.");
                    return;
                }

                try
                {
                    // Get the Unity project's Assets folder path
                    string assetsPath = Application.dataPath;
                    
                    AddSystemMessage($"🔄 Starting project parse for: {assetsPath}");
                    Debug.Log($"ParseProject: Assets path = {assetsPath}");
                    
                    // Create the request using proper class instead of anonymous object
                    var parseRequest = new ParseProjectRequest(assetsPath);
                    
                    Debug.Log($"ParseProject: Calling API with request = {Newtonsoft.Json.JsonConvert.SerializeObject(parseRequest)}");
                    
                    // Call the parse endpoint with correct path
                    var result = await serviceClient.PostAsync<object>("/parse", parseRequest);
                    
                    AddSystemMessage("✅ Project parsing completed successfully!");
                    Debug.Log($"ParseProject: Success - {result}");
                }
                catch (Exception ex)
                {
                    AddSystemMessage($"❌ Failed to parse project: {ex.Message}");
                    Debug.LogError($"Parse project error: {ex}");
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

            private VisualElement CreateCodeContextItem(CodeContext context, int index)
            {
                string extension = Path.GetExtension(context.filePath).ToLower();
                
                if (extension == ".asset")
                    return CreateAssetLink(context, index);
                else
                    return CreateCodeContextFoldout(context, index);
            }

            private VisualElement CreateAssetLink(CodeContext context, int index)
            {
                var container = new VisualElement();
                container.style.marginBottom = 5;
                container.style.marginLeft = 18;

                var linkLabel = new Label($"{index}. {context.GetTitle()}");

                linkLabel.RegisterCallback<ClickEvent>(evt => {
                    // Open in Unity Editor
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/" + context.filePath);
                    if (asset != null)
                    {
                        AssetDatabase.OpenAsset(asset);
                    }
                });

                linkLabel.style.color = new Color(0.7f, 0.9f, 1f);
                
                container.Add(linkLabel);
                return container;
            }
            
            private void CreateCodeContextSection(VisualElement container, List<CodeContext> codeContexts)
            {
                var codeSection = new VisualElement();
                codeSection.style.marginTop = 10;
                codeSection.style.paddingLeft = 15;
                
                // Section header
                var sectionHeaderLabel = new Label($"📄 Code Context ({codeContexts.Count} files)");
                sectionHeaderLabel.style.fontSize = 14;
                sectionHeaderLabel.style.color = new Color(0.7f, 0.9f, 1f);
                sectionHeaderLabel.style.marginBottom = 10;
                codeSection.Add(sectionHeaderLabel);
                
                // Create individual foldout for each code context
                for (int i = 0; i < codeContexts.Count; i++)
                {
                    var context = codeContexts[i];
                    var contextFoldout = CreateCodeContextItem(context, i + 1);
                    codeSection.Add(contextFoldout);
                }
                
                container.Add(codeSection);
            }
            
            // NEW: Create individual foldout for each code context
            private Foldout CreateCodeContextFoldout(CodeContext context, int index)
            {
                var foldout = new Foldout();
                foldout.text = $"{index}. {context.GetTitle()}";
                foldout.value = false; // Collapsed by default to save space
                foldout.style.fontSize = 12;
                foldout.style.marginBottom = 5;
                
                // Create the code block content
                var codeBlock = CreateCodeBlock(context);
                foldout.Add(codeBlock);
                
                return foldout;
            }
            
            // NEW: Create individual code block with syntax highlighting
            private VisualElement CreateCodeBlock(CodeContext context)
            {
                var blockContainer = new VisualElement();
                blockContainer.style.marginBottom = 10;
                blockContainer.style.marginTop = 5;
                blockContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                blockContainer.style.paddingLeft = 10;
                blockContainer.style.paddingRight = 10;
                blockContainer.style.paddingTop = 8;
                blockContainer.style.paddingBottom = 8;
                
                // File path info (smaller, since it's already in the foldout title)
                var fileInfo = new Label($"Lines {context.startLine}-{context.endLine}");
                fileInfo.style.fontSize = 10;
                fileInfo.style.color = new Color(0.6f, 0.6f, 0.6f);
                fileInfo.style.marginBottom = 8;
                blockContainer.Add(fileInfo);
                
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
            
            private void UpdateConnectionStatus(bool connected, string status)
            {
                serviceConnected = connected;
                serviceStatus = status;
                
                SetSendButtonEnabled(connected);
                // Note: Connection button is now handled by ServiceStatusIndicator
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
