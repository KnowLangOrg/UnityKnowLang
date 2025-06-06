using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityKnowLang.Editor
{
    /// <summary>
    /// HTTP client for communicating with the KnowLang Python service
    /// </summary>
    public class ServiceClient
    {
        private readonly string baseUrl;
        private readonly int timeoutSeconds;

        public ServiceClient(string baseUrl, int timeoutSeconds = 30)
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
                
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

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
                
                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

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

        public async Task<string> PostStreamAsync(string endpoint, object data, Action<string> onChunkReceived)
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
                request.SetRequestHeader("Accept", "text/event-stream");
                
                var operation = request.SendWebRequest();
                
                StringBuilder fullResponse = new StringBuilder();
                
                while (!operation.isDone)
                {
                    // Process streaming data if available
                    if (request.downloadHandler.text.Length > fullResponse.Length)
                    {
                        string newData = request.downloadHandler.text.Substring(fullResponse.Length);
                        fullResponse.Append(newData);
                        onChunkReceived?.Invoke(newData);
                    }
                    
                    await Task.Yield();
                }

                // Process any remaining data
                if (request.downloadHandler.text.Length > fullResponse.Length)
                {
                    string finalData = request.downloadHandler.text.Substring(fullResponse.Length);
                    fullResponse.Append(finalData);
                    onChunkReceived?.Invoke(finalData);
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return fullResponse.ToString();
                }
                else
                {
                    throw new Exception($"Stream request failed: {request.error}");
                }
            }
        }
    }
}