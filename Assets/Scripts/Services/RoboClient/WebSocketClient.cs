using System;
using System.Threading.Tasks;
using UnityEngine;
using MikeSchweitzer.WebSocket;

namespace RobotControl
{
    public abstract class WebSocketClient : IDisposable
    {
        protected WebSocketConnection _webSocket;
        protected readonly string _baseUrl;
        protected bool _isConnected;
        protected bool _disposed = false;

        public bool IsConnected => _isConnected;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<string> MessageReceived;

        protected WebSocketClient(string ipAddress, int port, string path)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("IP-адрес не може бути порожнім", nameof(ipAddress));
            
            if (port <= 0 || port > 65535)
                throw new ArgumentException("Некорректний порт", nameof(port));
                
            _baseUrl = $"wss://{ipAddress}:{port}/{path}";
            _isConnected = false;
            
            // Создаем объект WebSocketConnection
            GameObject go = new GameObject("WebSocketConnection");
            _webSocket = go.AddComponent<WebSocketConnection>();
            _webSocket.DesiredConfig = new WebSocketConfig
            {
                Url = _baseUrl
            };
            
            // Подписываемся на события
            _webSocket.StateChanged += OnWebSocketStateChanged;
            _webSocket.MessageReceived += OnWebSocketMessageReceived;
            _webSocket.ErrorMessageReceived += OnWebSocketErrorMessageReceived;
        }

        private void OnWebSocketStateChanged(WebSocketConnection connection, WebSocketState oldState, WebSocketState newState)
        {
            switch (newState)
            {
                case WebSocketState.Connected:
                    _isConnected = true;
                    OnConnectionStatusChanged("Підключено");
                    break;
                case WebSocketState.Disconnected:
                    _isConnected = false;
                    OnConnectionStatusChanged("Відключено");
                    break;
                case WebSocketState.Connecting:
                    OnConnectionStatusChanged("Підключення...");
                    break;
                case WebSocketState.Disconnecting:
                    OnConnectionStatusChanged("Відключення...");
                    break;
            }
        }

        private void OnWebSocketMessageReceived(WebSocketConnection connection, WebSocketMessage message)
        {
            string textMessage = message.String;
            MessageReceived?.Invoke(this, textMessage);
            OnMessageReceived(textMessage);
        }

        private void OnWebSocketErrorMessageReceived(WebSocketConnection connection, string errorMessage)
        {
            _isConnected = false;
            OnErrorOccurred($"Помилка WebSocket: {errorMessage}");
            OnConnectionStatusChanged("Помилка підключення");
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _webSocket.Connect();
                
                // Ждем подключения или ошибки
                int attempts = 0;
                while (!_isConnected && attempts < 30) // Увеличиваем до 30 попыток
                {
                    await Task.Delay(1000); // Увеличиваем задержку до 1 секунды
                    attempts++;
                }
                
                return _isConnected;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnErrorOccurred($"Помилка підключення: {ex.Message}");
                OnConnectionStatusChanged("Відключено");
                return false;
            }
        }

        protected abstract void OnMessageReceived(string message);

        public async Task<bool> SendMessageAsync(string message)
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Немає з'єднання з сервером");
                return false;
            }

            try
            {
                _webSocket.AddOutgoingMessage(message);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Помилка відправки повідомлення: {ex.Message}");
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_webSocket != null && _isConnected)
            {
                _webSocket.Disconnect();
            }
            _isConnected = false;
            OnConnectionStatusChanged("Відключено");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_webSocket != null)
                    {
                        _webSocket.Disconnect();
                        GameObject.Destroy(_webSocket.gameObject);
                    }
                }
                _disposed = true;
            }
        }

        protected virtual void OnConnectionStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        protected virtual void OnErrorOccurred(string errorMessage)
        {
            ErrorOccurred?.Invoke(this, errorMessage);
        }
    }
} 