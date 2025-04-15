using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net;

namespace RobotControl
{
    public abstract class WebSocketClient : IDisposable
    {
        protected ClientWebSocket _webSocket;
        protected readonly string _baseUrl;
        protected bool _isConnected;
        protected bool _disposed = false;
        protected CancellationTokenSource _cts;
        private readonly string _serverThumbprint;

        public bool IsConnected => _isConnected;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<string> MessageReceived;

        protected WebSocketClient(string ipAddress, int port, string path, string serverThumbprint)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("IP-адрес не може бути порожнім", nameof(ipAddress));
            
            if (port <= 0 || port > 65535)
                throw new ArgumentException("Некорректний порт", nameof(port));

            if (string.IsNullOrWhiteSpace(serverThumbprint))
                throw new ArgumentException("Отпечаток сертификата сервера не может быть пустым", nameof(serverThumbprint));
                
            _baseUrl = $"wss://{ipAddress}:{port}/{path}";
            _serverThumbprint = serverThumbprint.Replace(":", "").ToUpper();
            _isConnected = false;
            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            // Настраиваем проверку сертификата
            _webSocket.Options.RemoteCertificateValidationCallback = ValidateServerCertificate;
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // Проверяем, что сертификат соответствует ожидаемому
            if (certificate is X509Certificate2 cert2)
            {
                string certThumbprint = cert2.Thumbprint.ToUpper();
                if (certThumbprint == _serverThumbprint)
                {
                    Debug.Log("Сертификат сервера успешно проверен");
                    return true;
                }
                else
                {
                    Debug.LogError($"Неверный отпечаток сертификата. Ожидался: {_serverThumbprint}, получен: {certThumbprint}");
                }
            }

            Debug.LogWarning($"Ошибка проверки сертификата: {sslPolicyErrors}");
            return false;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    Debug.Log("WebSocket уже подключен");
                    return true;
                }

                OnConnectionStatusChanged("Підключення...");
                
                // Очищаем предыдущее подключение если оно было
                if (_webSocket.State != WebSocketState.None)
                {
                    await DisconnectAsync();
                    _webSocket = new ClientWebSocket();
                }

                // Пробуем подключиться несколько раз
                int maxAttempts = 3;
                int currentAttempt = 0;
                
                while (currentAttempt < maxAttempts)
                {
                    try
                    {
                        Debug.Log($"Попытка подключения {currentAttempt + 1} из {maxAttempts}");
                        await _webSocket.ConnectAsync(new Uri(_baseUrl), _cts.Token);
                        
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            _isConnected = true;
                            OnConnectionStatusChanged("Підключено");
                            Debug.Log("WebSocket успешно подключен");
                            
                            // Запускаем прослушивание сообщений
                            _ = StartListening();
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Ошибка подключения (попытка {currentAttempt + 1}): {ex.Message}");
                        if (currentAttempt == maxAttempts - 1)
                        {
                            throw;
                        }
                        await Task.Delay(1000); // Ждем 1 секунду перед следующей попыткой
                    }
                    currentAttempt++;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                string errorMessage = $"Помилка підключення: {ex.Message}";
                Debug.LogError(errorMessage);
                OnErrorOccurred(errorMessage);
                OnConnectionStatusChanged("Відключено");
                return false;
            }
        }

        private async Task StartListening()
        {
            var buffer = new byte[4096];
            while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        MessageReceived?.Invoke(this, message);
                        OnMessageReceived(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _isConnected = false;
                        OnConnectionStatusChanged("Відключено");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _isConnected = false;
                    OnErrorOccurred($"Помилка отримання повідомлення: {ex.Message}");
                    OnConnectionStatusChanged("Відключено");
                    break;
                }
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
                byte[] bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
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
                try
                {
                    OnConnectionStatusChanged("Відключення...");
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", _cts.Token);
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Помилка відключення: {ex.Message}");
                }
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
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _webSocket?.Dispose();
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