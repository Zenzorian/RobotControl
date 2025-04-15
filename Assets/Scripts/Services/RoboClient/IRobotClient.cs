using UnityEngine;
using System;
using System.Threading.Tasks;
namespace RobotControl
{
    public interface IRobotClient : IDisposable
    {
        // События подключения
        event EventHandler<string> ConnectionStatusChanged;
        event EventHandler<string> ErrorOccurred;

        // Свойства
        bool IsConnected { get; }    

        void InitializeConnection(string ipAddress, int port);    

        // Методы управления
        Task<bool> ConnectAsync(string ipAddress, int port);
        Task DisconnectAsync();
        Task<bool> SendMovementCommandAsync(int leftWheels, int rightWheels, int speed);
        Task<bool> SendCameraCommandAsync(int angle);
        Task<bool> SendStopCommand();
        Task<bool> InitiateWebRTCConnection();
        Task<bool> SendVideoSettingsAsync(int quality, int fps);

        // События видео
        event EventHandler<Texture2D> VideoFrameReceived;
        event EventHandler<WebRTCSignalingMessage> WebRTCSignalingReceived;
    }
} 