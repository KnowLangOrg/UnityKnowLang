using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace UnityKnowLang.Editor
{
    /// <summary>
    /// Minimal Mixpanel service for Unity Editor event tracking
    /// Single responsibility: Track events with minimal setup
    /// </summary>
    public static class MixpanelService
    {
        // Replace with your actual Mixpanel project token
        private const string MIXPANEL_TOKEN = "ec5727f25280a135d663a4257a2430b8";
        private const string TRACK_URL = "https://api.mixpanel.com/track";
        
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string _distinctId = SystemInfo.deviceUniqueIdentifier;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the service (called automatically on first use)
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                Debug.Log($"MixpanelService initialized with DistinctId: {_distinctId}");
            }
        }

        /// <summary>
        /// Track an event with optional properties
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="properties">Optional event properties</param>
        public static void TrackEvent(string eventName, Dictionary<string, object> properties = null)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogWarning("MixpanelService: Event name cannot be null or empty");
                return;
            }

            EnsureInitialized();
            _ = TrackEventAsync(eventName, properties);
        }

        /// <summary>
        /// Track an event asynchronously
        /// </summary>
        private static async Task TrackEventAsync(string eventName, Dictionary<string, object> properties)
        {
            try
            {
                var eventData = CreateEventData(eventName, properties);
                var jsonPayload = JsonConvert.SerializeObject(new[] { eventData });
                var base64Payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonPayload));

                var formData = new Dictionary<string, string>
                {
                    {"data", base64Payload}
                };

                var formContent = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(TRACK_URL, formContent);
                
                if (response.IsSuccessStatusCode)
                {
                    Debug.Log($"MixpanelService: Event '{eventName}' tracked successfully");
                }
                else
                {
                    Debug.LogWarning($"MixpanelService: Failed to track event '{eventName}'. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"MixpanelService: Error tracking event '{eventName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Create event data structure
        /// </summary>
        private static Dictionary<string, object> CreateEventData(string eventName, Dictionary<string, object> properties)
        {
            // Default Mixpanel event properties
            var eventProperties = new Dictionary<string, object>
            {
                {"token", MIXPANEL_TOKEN},
                {"distinct_id", _distinctId},
                {"time", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},
                {"$lib_version", "1.0.0"},
                {"$os", SystemInfo.operatingSystemFamily.ToString()},
                {"unity_version", Application.unityVersion},
            };

            // Add custom properties
            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    eventProperties[kvp.Key] = kvp.Value;
                }
            }

            return new Dictionary<string, object>
            {
                {"event", eventName},
                {"properties", eventProperties}
            };
        }

        // Convenience methods for common plugin events
        public static void TrackPluginOpened()
        {
            TrackEvent("Plugin Opened");
        }

        public static void TrackParseProjectClicked()
        {
            TrackEvent("Parse Project Clicked");
        }

        public static void TrackStreamChatRequested()
        {
            TrackEvent("Stream Chat Requested");
        }

        public static void TrackRetrievedContextClick()
        {
            TrackEvent("Retrieved Context Clicked");
        }
    }
}