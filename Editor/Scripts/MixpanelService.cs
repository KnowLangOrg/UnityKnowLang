using Mixpanel;
using UnityEngine;
using System.Collections.Generic;

namespace UnityKnowLang.Editor
{
    public static class MixpanelService
    {
        private static MixpanelClient _client;
        private static string _userId;
        private const string MixpanelTokenPlaceholder = "YOUR_MIXPANEL_TOKEN_PLACEHOLDER";

        public static void Initialize()
        {
            if (_client != null)
            {
                Debug.Log("MixpanelService already initialized.");
                return;
            }

            _userId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(_userId))
            {
                _userId = "editor_user_unknown"; // Fallback user ID
                Debug.LogWarning("MixpanelService: deviceUniqueIdentifier is null or empty. Using fallback ID.");
            }

            // In a real Unity environment, MixpanelClient might require specific initialization
            // for the Unity context, or it might work directly if its dependencies are met.
            // For now, we assume direct instantiation works.
            _client = new MixpanelClient(MixpanelTokenPlaceholder);
            _client.Identify(_userId);

            Debug.Log($"MixpanelService Initialized. User ID: {_userId}, Token: {MixpanelTokenPlaceholder}");
        }

        public static void TrackEvent(string eventName, IDictionary<string, object> properties = null)
        {
            if (_client == null)
            {
                Debug.LogError("MixpanelService not initialized. Call Initialize() first.");
                return;
            }

            if (properties == null)
            {
                properties = new Dictionary<string, object>();
            }

            // Add some default properties if needed, e.g., app version
            // properties["$app_version_string"] = Application.version; // Example for Unity runtime

            _client.Track(eventName, _userId, properties);
            Debug.Log($"Mixpanel Event Tracked: {eventName}, User ID: {_userId}, Properties: {JsonUtility.ToJson(properties)}");
        }

        // Example of a more specific tracking method if needed in the future
        public static void TrackParseProjectButtonClicked()
        {
            TrackEvent("Parse Project Button Clicked");
        }

        public static void TrackStreamChatRequestSent()
        {
            TrackEvent("Stream Chat Request Sent");
        }
    }
}
