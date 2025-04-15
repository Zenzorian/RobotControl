using UnityEngine;
using System;
using System.Threading.Tasks;
using UnityEngine.Events;

namespace RobotControl
{
    public class RobotClient : WebSocketClient, IRobotClient
    {
        private RobotControlClient _controlClient;
        private RobotVideoClient _videoClient;
        private readonly string _ipAddress;
        private readonly int _port;
        private readonly string _path;
        private readonly string _serverThumbprint;
        private bool _isConnected;
        private UnityEvent<string, int> _onConnectButtonClicked;

        public bool IsConnected => _isConnected;  
        
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<Texture2D> VideoFrameReceived;
        public event EventHandler<WebRTCSignalingMessage> WebRTCSignalingReceived;

        public RobotClient(UnityEvent<string, int> onConnectButtonClicked, string serverThumbprint)
            : base("", 0, "", serverThumbprint)
        {
            _isConnected = false;
            _onConnectButtonClicked = onConnectButtonClicked;           
            _onConnectButtonClicked.AddListener(InitializeConnection);
            _serverThumbprint = serverThumbprint;
        }

        protected override void OnMessageReceived(string message)
        {
            // Обработка сообщений не требуется, так как используются отдельные клиенты
        }

        public async void InitializeConnection(string ipAddress, int port)
        {  
            _isConnected = await ConnectAsync(ipAddress, port);           
        }

        public async Task<bool> ConnectAsync(string ipAddress, int port)
        {          
            try
            {
                Debug.Log("Starting connection process...");
                // Создаем клиенты с проверкой сертификата
                _controlClient = new RobotControlClient(ipAddress, port, _serverThumbprint);
                _videoClient = new RobotVideoClient(ipAddress, port, _serverThumbprint);
                Debug.Log("Clients created");

                // Подписываемся на события
                _controlClient.ConnectionStatusChanged += OnConnectionStatusChanged;
                _controlClient.ErrorOccurred += OnErrorOccurred;
                _videoClient.ConnectionStatusChanged += OnConnectionStatusChanged;
                _videoClient.ErrorOccurred += OnErrorOccurred;
                _videoClient.VideoFrameReceived += OnVideoFrameReceived;
                _videoClient.WebRTCSignalingReceived += OnWebRTCSignalingReceived;
                Debug.Log("Event handlers subscribed");

                // Подключаемся
                Debug.Log("Connecting control client...");
                bool controlConnected = await _controlClient.ConnectAsync();
                Debug.Log($"Control client connected: {controlConnected}");
                
                Debug.Log("Connecting video client...");
                bool videoConnected = await _videoClient.ConnectAsync();
                Debug.Log($"Video client connected: {videoConnected}");

                _isConnected = controlConnected && videoConnected;
                Debug.Log($"Final connection status: {_isConnected}");

                if (_isConnected)
                {
                    OnConnectionStatusChanged(this, "Підключено");
                }
                else
                {
                    OnErrorOccurred(this, "Помилка підключення до сервера");
                }

                return _isConnected;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Connection error: {ex.Message}");
                OnErrorOccurred(this, $"Помилка підключення: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_controlClient != null)
            {
                await _controlClient.DisconnectAsync();
            }

            if (_videoClient != null)
            {
                await _videoClient.DisconnectAsync();
            }

            _isConnected = false;
            OnConnectionStatusChanged(this, "Відключено");
        }

        public async Task<bool> SendMovementCommandAsync(int leftWheels, int rightWheels, int speed)
        {
            if (!_isConnected)
            {
                OnErrorOccurred(this, "Немає з'єднання з роботом");
                return false;
            }

            return await _controlClient.SendMovementCommandAsync(leftWheels, rightWheels, speed);
        }

        public async Task<bool> SendCameraCommandAsync(int angle)
        {
            if (!_isConnected)
            {
                OnErrorOccurred(this, "Немає з'єднання з роботом");
                return false;
            }

            return await _controlClient.SendCameraCommandAsync(angle);
        }

        public async Task<bool> SendStopCommand()
        {
            if (!_isConnected)
            {
                OnErrorOccurred(this, "Немає з'єднання з роботом");
                return false;
            }

            return await _controlClient.SendStopCommand();
        }

        public async Task<bool> InitiateWebRTCConnection()
        {
            if (!_isConnected)
            {
                OnErrorOccurred(this, "Немає з'єднання з роботом");
                return false;
            }

            return await _videoClient.InitiateWebRTCConnection();
        }

        public async Task<bool> SendVideoSettingsAsync(int quality, int fps)
        {
            if (!_isConnected)
            {
                OnErrorOccurred(this, "Немає з'єднання з роботом");
                return false;
            }

            return await _videoClient.SendVideoSettingsAsync(quality, fps);
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        private void OnErrorOccurred(object sender, string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        private void OnVideoFrameReceived(object sender, Texture2D frame)
        {
            VideoFrameReceived?.Invoke(this, frame);
        }

        private void OnWebRTCSignalingReceived(object sender, WebRTCSignalingMessage message)
        {
            WebRTCSignalingReceived?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (_controlClient != null)
            {
                _controlClient.Dispose();
            }

            if (_videoClient != null)
            {
                _videoClient.Dispose();
            }
        }
    }
} 