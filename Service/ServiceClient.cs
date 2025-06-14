using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;

namespace UnityKnowLang.Editor
{
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
        public ChatStatus status;
        public string progress_message;
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
            string jsonData = JsonUtility.ToJson(data);

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
                    return JsonUtility.FromJson<T>(json);
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
            string wsUrl = $"{baseUrl}/ws/chat/stream";

            WebSocket webSocket = null;
            TaskCompletionSource<StreamingChatResult> completionSource = new TaskCompletionSource<StreamingChatResult>();
            StreamingChatResult finalResult = null;

            try
            {
                webSocket = new WebSocket(wsUrl);

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
                        var chatResult = JsonUtility.FromJson<StreamingChatResult>(message);

                        if (chatResult != null)
                        {
                            // Invoke callback for progress updates
                            onMessageReceived?.Invoke(chatResult);

                            // Check if this is the final message
                            if (chatResult.status == ChatStatus.COMPLETE || chatResult.status == ChatStatus.ERROR)
                            {
                                finalResult = chatResult;
                                completionSource.SetResult(chatResult);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error parsing WebSocket message: {ex.Message}");
                        var errorResult = new StreamingChatResult
                        {
                            answer = $"Error parsing server response: {ex.Message}",
                            status = ChatStatus.ERROR,
                            progress_message = "Client-side parsing error"
                        };
                        completionSource.SetResult(errorResult);
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
                    completionSource.SetResult(errorResult);
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
                        completionSource.SetResult(errorResult);
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
    }
}