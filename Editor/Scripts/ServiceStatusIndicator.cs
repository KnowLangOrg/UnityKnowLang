using UnityEngine;
using UnityEngine.UIElements;

namespace UnityKnowLang.Editor
{
    /// <summary>
    /// Visual component for displaying service status in Unity UI
    /// </summary>
    public class ServiceStatusIndicator : VisualElement
    {
        private readonly Label statusLabel;
        private readonly Button actionButton;
        private readonly VisualElement statusDot;
        private KnowLangServerManager serviceManager;

        public ServiceStatusIndicator()
        {
            // Create the UI structure
            style.flexDirection = FlexDirection.Row;
            style.justifyContent = Justify.Center;
            style.alignItems = Align.Center;
            style.minHeight = 28; // Match header button height
            style.flexShrink = 0; // Don't compress the status indicator itself

            // Status dot
            statusDot = new VisualElement();
            statusDot.style.width = 10;
            statusDot.style.height = 10;
            statusDot.style.marginRight = 8;
            statusDot.style.flexShrink = 0;
            Add(statusDot);

            // Status label
            statusLabel = new Label("Service Stopped");
            statusLabel.style.flexGrow = 1;
            statusLabel.style.flexShrink = 1; // Allow text to compress
            statusLabel.style.minWidth = 80; // Ensure minimum readable width
            statusLabel.style.fontSize = 12;
            statusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            statusLabel.style.marginRight = 8;
            Add(statusLabel);

            // Action button with responsive sizing
            actionButton = new Button(OnActionButtonClicked)
            {
                text = "Start"
            };
            actionButton.style.minWidth = 50;
            actionButton.style.maxWidth = 80;
            actionButton.style.height = 26;
            actionButton.style.flexShrink = 1;
            actionButton.style.fontSize = 11;
            Add(actionButton);

            UpdateDisplay(ServiceStatus.Stopped);
        }

        public void SetServiceManager(KnowLangServerManager manager)
        {
            if (serviceManager != null)
            {
                serviceManager.OnStatusChanged -= OnStatusChanged;
            }

            serviceManager = manager;
            
            if (serviceManager != null)
            {
                serviceManager.OnStatusChanged += OnStatusChanged;
                UpdateDisplay(serviceManager.Status);
            }
        }

        private void OnStatusChanged(ServiceStatus status)
        {
            UpdateDisplay(status);
        }

        private void UpdateDisplay(ServiceStatus status)
        {
            switch (status)
            {
                case ServiceStatus.Stopped:
                    statusDot.style.backgroundColor = Color.red;
                    statusLabel.text = "Stopped";
                    actionButton.text = "Start";
                    actionButton.SetEnabled(true);
                    break;
                    
                case ServiceStatus.Starting:
                    statusDot.style.backgroundColor = Color.yellow;
                    statusLabel.text = "Starting...";
                    actionButton.text = "Starting";
                    actionButton.SetEnabled(false);
                    break;
                    
                case ServiceStatus.Running:
                    statusDot.style.backgroundColor = Color.green;
                    statusLabel.text = "Running";
                    actionButton.text = "Stop";
                    actionButton.SetEnabled(true);
                    break;
                    
                case ServiceStatus.Stopping:
                    statusDot.style.backgroundColor = Color.yellow;
                    statusLabel.text = "Stopping...";
                    actionButton.text = "Stopping";
                    actionButton.SetEnabled(false);
                    break;
                    
                case ServiceStatus.Error:
                    statusDot.style.backgroundColor = Color.red;
                    statusLabel.text = "Error";
                    actionButton.text = "Restart";
                    actionButton.SetEnabled(true);
                    break;
            }
        }

        private async void OnActionButtonClicked()
        {
            if (serviceManager == null) return;

            switch (serviceManager.Status)
            {
                case ServiceStatus.Stopped:
                case ServiceStatus.Error:
                    await serviceManager.StartServiceAsync();
                    break;
                    
                case ServiceStatus.Running:
                    serviceManager.StopService();
                    break;
            }
        }
    }
}