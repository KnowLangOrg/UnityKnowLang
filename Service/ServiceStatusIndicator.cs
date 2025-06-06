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
        private PythonServiceManager serviceManager;

        public ServiceStatusIndicator()
        {
            // Create the UI structure
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = 5;

            // Status dot
            statusDot = new VisualElement();
            statusDot.style.width = 12;
            statusDot.style.height = 12;
            statusDot.style.marginRight = 5;
            Add(statusDot);

            // Status label
            statusLabel = new Label("Service Stopped");
            statusLabel.style.flexGrow = 1;
            Add(statusLabel);

            // Action button
            actionButton = new Button(OnActionButtonClicked)
            {
                text = "Start"
            };
            actionButton.style.width = 60;
            Add(actionButton);

            UpdateDisplay(ServiceStatus.Stopped);
        }

        public void SetServiceManager(PythonServiceManager manager)
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
                    statusLabel.text = "Service Stopped";
                    actionButton.text = "Start";
                    actionButton.SetEnabled(true);
                    break;
                    
                case ServiceStatus.Starting:
                    statusDot.style.backgroundColor = Color.yellow;
                    statusLabel.text = "Service Starting...";
                    actionButton.text = "Starting";
                    actionButton.SetEnabled(false);
                    break;
                    
                case ServiceStatus.Running:
                    statusDot.style.backgroundColor = Color.green;
                    statusLabel.text = $"Service Running ({serviceManager?.ServiceUrl})";
                    actionButton.text = "Stop";
                    actionButton.SetEnabled(true);
                    break;
                    
                case ServiceStatus.Stopping:
                    statusDot.style.backgroundColor = Color.yellow;
                    statusLabel.text = "Service Stopping...";
                    actionButton.text = "Stopping";
                    actionButton.SetEnabled(false);
                    break;
                    
                case ServiceStatus.Error:
                    statusDot.style.backgroundColor = Color.red;
                    statusLabel.text = "Service Error";
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