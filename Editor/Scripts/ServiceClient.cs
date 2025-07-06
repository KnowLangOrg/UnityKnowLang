using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using UnityEditor;


namespace UnityKnowLang.Editor
{
    [System.Serializable]
    public class CodeContext
    {
        public string filePath;
        public int startLine;
        public int endLine;
        public string code;
        
        public string GetTitle()
        {
            return $"📄 {System.IO.Path.GetFileName(filePath)} (lines {startLine}-{endLine})";
        }
    }


    /// <summary>
    /// Chat status enum matching Python ChatStatus
    /// </summary>
    [Serializable]
    public enum ChatStatus
    {
        STARTING,
        POLISHING,
        RETRIEVING,
        ANSWERING,
        COMPLETE,
        ERROR
    }


    /// <summary>
    /// Search result data structure
    /// </summary>
    [Serializable]
    public class SearchResult
    {
        public string document;
        public float score;
        public Dictionary<string, object> metadata;
    }

    /// <summary>
    /// Streaming chat result matching Python StreamingChatResult
    /// </summary>
    [Serializable]
    public class StreamingChatResult
    {
        public string answer;
        public List<SearchResult> retrieved_context;
        
        [JsonConverter(typeof(ChatStatusConverter))]
        public ChatStatus status = ChatStatus.STARTING;

        public string progress_message;
    }

    // Alternative: Custom JsonConverter if you need specific string mapping
    public class ChatStatusConverter : JsonConverter<ChatStatus>
    {
        public override ChatStatus ReadJson(JsonReader reader, Type objectType, ChatStatus existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string value = reader.Value?.ToString();
            return value?.ToLower() switch
            {
                "starting" => ChatStatus.STARTING,
                "retrieving" => ChatStatus.RETRIEVING,
                "polishing" => ChatStatus.POLISHING,
                "answering" => ChatStatus.ANSWERING,
                "complete" => ChatStatus.COMPLETE,
                "error" => ChatStatus.ERROR,
                _ => ChatStatus.STARTING
            };
        }

        public override void WriteJson(JsonWriter writer, ChatStatus value, JsonSerializer serializer)
        {
            string stringValue = value switch
            {
                ChatStatus.STARTING => "starting",
                ChatStatus.RETRIEVING => "retrieving",
                ChatStatus.POLISHING => "polishing",
                ChatStatus.ANSWERING => "answering",
                ChatStatus.COMPLETE => "complete",
                ChatStatus.ERROR => "error",
                _ => "starting"
            };
            writer.WriteValue(stringValue);
        }
    }


    /// <summary>
    /// Server-sent chat event matching Python ServerSentChatEvent
    /// </summary>
    [Serializable]
    public class ServerSentChatEvent
    {
        public ChatStatus event_status; // renamed from 'event' to avoid C# keyword
        public StreamingChatResult data;
    }

    /// <summary>
    /// HTTP client for communicating with the KnowLang Python service
    /// </summary>
    public class ServiceClient
    {
        private readonly string baseUrl;
        private readonly int timeoutSeconds;
        private WebSocket activeWebSocket;
        private bool isDispatchingMessages;

        public ServiceClient(string baseUrl, int timeoutSeconds = 120)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.timeoutSeconds = timeoutSeconds;
        }

        public async Task<T> GetAsync<T>(string endpoint) where T : class
        {
            string url = $"{baseUrl}/{endpoint.TrimStart('/')}";

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    return JsonUtility.FromJson<T>(json);
                }
                else
                {
                    throw new Exception($"Request failed: {request.error}\nResponse: {request.downloadHandler.text}");
                }
            }
        }

        public async Task<T> PostAsync<T>(string endpoint, object data) where T : class
        {
            string url = $"{baseUrl}/{endpoint.TrimStart('/')}";
            // Use Newtonsoft.Json for proper serialization of anonymous objects
            string jsonData = JsonConvert.SerializeObject(data);
            
            Debug.Log($"POST {url} with data: {jsonData}"); // Debug logging

            using (var request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    if (typeof(T) == typeof(object))
                    {
                        // For generic object return type, just return a dummy object
                        return (T)(object)new { success = true };
                    }
                    return JsonConvert.DeserializeObject<T>(json);
                }
                else
                {
                    throw new Exception($"Request failed: {request.error}\nResponse: {request.downloadHandler.text}");
                }
            }
        }

        public async Task<StreamingChatResult> StreamChatAsync(
            string query,
            Action<StreamingChatResult> onMessageReceived = null,
            CancellationToken cancellationToken = default)
        {
            // Convert HTTP URL to WebSocket URL
            string wsUrl = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
            wsUrl = $"{wsUrl}/ws/chat/stream";

            WebSocket webSocket = null;
            TaskCompletionSource<StreamingChatResult> completionSource = new TaskCompletionSource<StreamingChatResult>();
            StreamingChatResult finalResult = null;

            try
            {

                webSocket = new WebSocket(wsUrl);
                activeWebSocket = webSocket; // Store the active WebSocket
                StartMessageDispatching();
                // Set up event handlers
                webSocket.OnOpen += () =>
                {
                    Debug.Log("WebSocket connection opened");
                    // Send the query immediately after connection opens
                    webSocket.SendText(query);
                };

                webSocket.OnMessage += (bytes) =>
                {
                    try
                    {
                        string message = Encoding.UTF8.GetString(bytes);
                        Debug.Log($"Received WebSocket message: {message}");

                        // Parse the JSON message
                        // var chatResult = JsonUtility.FromJson<StreamingChatResult>(message);
                        var chatResult = JsonConvert.DeserializeObject<StreamingChatResult>(message);

                        if (chatResult != null)
                        {
                            // Invoke callback for progress updates
                            onMessageReceived?.Invoke(chatResult);

                            // Check if this is the final message
                            if (chatResult.status == ChatStatus.COMPLETE ||
                                chatResult.status == ChatStatus.ERROR ||
                                (!string.IsNullOrEmpty(chatResult.answer) && chatResult.answer.Contains("error processing")))
                            {
                                finalResult = chatResult;
                                completionSource.SetResult(chatResult);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Error parsing WebSocket message: {ex.Message}");
                        var errorResult = new StreamingChatResult
                        {
                            answer = $"Error parsing server response: {ex.Message}",
                            status = ChatStatus.ERROR,
                            progress_message = "Client-side parsing error"
                        };
                        completionSource.TrySetResult(errorResult);
                    }
                };

                webSocket.OnError += (errorMsg) =>
                {
                    Debug.LogError($"WebSocket error: {errorMsg}");
                    var errorResult = new StreamingChatResult
                    {
                        answer = $"WebSocket error: {errorMsg}",
                        status = ChatStatus.ERROR,
                        progress_message = "Connection error"
                    };
                    completionSource.TrySetResult(errorResult);
                };

                webSocket.OnClose += (closeCode) =>
                {
                    Debug.Log($"WebSocket connection closed with code: {closeCode}");

                    // If we haven't completed yet, this might be an unexpected close
                    if (!completionSource.Task.IsCompleted)
                    {
                        var errorResult = new StreamingChatResult
                        {
                            answer = "Connection closed unexpectedly",
                            status = ChatStatus.ERROR,
                            progress_message = "Connection terminated"
                        };
                        completionSource.TrySetResult(errorResult);
                    }
                };

                // Connect to WebSocket
                await webSocket.Connect();

                // Register cancellation callback
                cancellationToken.Register(() =>
                {
                    try
                    {
                        webSocket?.Close();
                        if (!completionSource.Task.IsCompleted)
                        {
                            completionSource.SetCanceled();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error during cancellation: {ex.Message}");
                    }
                });

                // Wait for completion or timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
                var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException($"WebSocket operation timed out after {timeoutSeconds} seconds");
                }

                return await completionSource.Task;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("WebSocket operation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"WebSocket streaming error: {ex.Message}");
                return new StreamingChatResult
                {
                    answer = $"Streaming error: {ex.Message}",
                    status = ChatStatus.ERROR,
                    progress_message = "Client-side error"
                };
            }
            finally
            {
                activeWebSocket = null;
                StopMessageDispatching();

                // Ensure WebSocket is properly closed
                try
                {
                    if (webSocket != null && webSocket.State == WebSocketState.Open)
                    {
                        await webSocket.Close();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error closing WebSocket: {ex.Message}");
                }
            }
        }

        #region ActiveWebSocket Management
        private void StartMessageDispatching()
        {
            if (!isDispatchingMessages)
            {
                isDispatchingMessages = true;
                EditorApplication.update += DispatchMessages;
            }
        }

        private void StopMessageDispatching()
        {
            if (isDispatchingMessages)
            {
                isDispatchingMessages = false;
                EditorApplication.update -= DispatchMessages;
            }
        }

        private void DispatchMessages()
        {
            activeWebSocket?.DispatchMessageQueue();
        }
        #endregion
    }
}