using UnityEngine;
using Scripts.UI;

namespace Scripts.Services
{
    public class Status : IStatus
    {
        private StatusMarker _statusMarker;
        public Status(StatusMarker statusMarker)
        {
            _statusMarker = statusMarker;
        }

        public void UpdateServerStatus(bool isConnected)
        {
            _statusMarker.serverStatusImage.color = isConnected ? _statusMarker.activeColor : _statusMarker.inactiveColor;            
        }
        public void UpdateRobotStatus(bool isConnected)
        {           
            _statusMarker.robotStatusImage.color = isConnected ? _statusMarker.activeColor : _statusMarker.inactiveColor;
        }      

        public void Error(string message)
        {
            Debug.Log($"Error: {message}");
            _statusMarker.debugText.color = _statusMarker.errorTextColor;
            _statusMarker.debugText.text = message;
        }

        public void Info(string message)
        {
            Debug.Log($"Info: {message}");
            _statusMarker.debugText.color = _statusMarker.infoTextColor;
           _statusMarker.debugText.text = message;
        }

        public void Warning(string message)
        {
            Debug.Log($"Warning: {message}");
            _statusMarker.debugText.color = Color.yellow; // Используем желтый цвет для предупреждений
            _statusMarker.debugText.text = message;
        }
    }
}