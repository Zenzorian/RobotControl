using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RobotClient.Config;

namespace RobotClient.Video
{
    /// <summary>
    /// Клиент для подключения робота к WebSocket серверу
    /// </summary>
    public class WebSocketClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly string _serverUrl;
        private bool _isConnected;
        private bool _isRegistered;
        
        public event Action<string>? MessageReceived;
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<string>? ConnectionError;

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;
        public bool IsRegistered => _isRegistered && IsConnected;

        public WebSocketClient(string? serverUrl = null)
        {
            _serverUrl = serverUrl ?? ServerConfig.WebSocketUrl;
            _isConnected = false;
            _isRegistered = false;
            
            Console.WriteLine($"🔌 WebSocket клиент создан для сервера: {_serverUrl}");
        }

        /// <summary>
        /// Подключение к WebSocket серверу
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                {
                    Console.WriteLine("⚠️ WebSocket уже подключен");
                    return true;
                }

                _webSocket?.Dispose();
                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                Console.WriteLine($"🔌 Подключение к серверу: {_serverUrl}");
                
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(ServerConfig.CONNECTION_TIMEOUT_SECONDS));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, timeoutCts.Token);
                
                await _webSocket.ConnectAsync(new Uri(_serverUrl), combinedCts.Token);
                
                _isConnected = true;
                Console.WriteLine("✅ WebSocket подключен");
                
                Connected?.Invoke();
                
                // Запускаем прослушивание сообщений
                _ = Task.Run(ListenForMessages);
                
                return true;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"❌ Таймаут подключения к серверу ({ServerConfig.CONNECTION_TIMEOUT_SECONDS}s)");
                _isConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка подключения WebSocket: {ex.Message}");
                ConnectionError?.Invoke(ex.Message);
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Регистрация робота на сервере
        /// </summary>
        public async Task<bool> RegisterAsRobotAsync()
        {
            if (!IsConnected)
            {
                Console.WriteLine("❌ WebSocket не подключен");
                return false;
            }

            try
            {
                Console.WriteLine("📝 Регистрация как ROBOT...");
                await SendMessageAsync("REGISTER!ROBOT");
                
                // Ждем подтверждения регистрации
                var registrationResult = await WaitForRegistrationConfirmation();
                
                if (registrationResult)
                {
                    _isRegistered = true;
                    Console.WriteLine("✅ Робот успешно зарегистрирован");
                }
                else
                {
                    Console.WriteLine("❌ Ошибка регистрации робота");
                }
                
                return registrationResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка регистрации: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отправка сообщения на сервер
        /// </summary>
        public async Task<bool> SendMessageAsync(string message)
        {
            if (!IsConnected)
            {
                Console.WriteLine("❌ WebSocket не подключен");
                return false;
            }

            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket!.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource!.Token
                );
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки сообщения: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отправка JSON сообщения (для WebRTC)
        /// </summary>
        public async Task<bool> SendJsonMessageAsync(object messageObject)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(messageObject, options);
                Console.WriteLine($"📤 Отправляемый JSON: {json}");
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки JSON: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отправка телеметрии
        /// </summary>
        public async Task<bool> SendTelemetryAsync(object telemetryData)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var telemetryJson = JsonSerializer.Serialize(telemetryData, options);
                var message = $"TELEMETRY!{telemetryJson}";
                return await SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки телеметрии: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ожидание подтверждения регистрации
        /// </summary>
        private async Task<bool> WaitForRegistrationConfirmation()
        {
            var timeoutTask = Task.Delay(5000); // 5 секунд таймаут
            var registrationTask = WaitForMessage("REGISTERED!ROBOT");

            var completedTask = await Task.WhenAny(timeoutTask, registrationTask);

            if (completedTask == registrationTask)
            {
                return await registrationTask;
            }
            else
            {
                Console.WriteLine("⏰ Таймаут ожидания регистрации");
                return false;
            }
        }

        /// <summary>
        /// Ожидание конкретного сообщения
        /// </summary>
        private async Task<bool> WaitForMessage(string expectedMessage)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            void OnMessageReceived(string message)
            {
                if (message == expectedMessage)
                {
                    MessageReceived -= OnMessageReceived;
                    tcs.SetResult(true);
                }
            }

            MessageReceived += OnMessageReceived;
            
            // Таймаут на случай, если сообщение не придет
            _ = Task.Delay(10000).ContinueWith(_ => {
                MessageReceived -= OnMessageReceived;
                tcs.TrySetResult(false);
            });

            return await tcs.Task;
        }

        /// <summary>
        /// Прослушивание входящих сообщений
        /// </summary>
        private async Task ListenForMessages()
        {
            var buffer = new byte[4096];
            
            try
            {
                while (IsConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    var result = await _webSocket!.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cancellationTokenSource.Token
                    );

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        Console.WriteLine($"📨 Получено сообщение: {message}");
                        
                        // Дополнительная диагностика для WebRTC сообщений
                        if (message.Contains("webrtc"))
                        {
                            Console.WriteLine($"🔍 WebRTC сообщение обнаружено!");
                        }
                        
                        MessageReceived?.Invoke(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("🔌 Сервер закрыл соединение");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🛑 Прослушивание сообщений отменено");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при прослушивании: {ex.Message}");
                ConnectionError?.Invoke(ex.Message);
            }
            finally
            {
                _isConnected = false;
                _isRegistered = false;
                Disconnected?.Invoke();
            }
        }

        /// <summary>
        /// Переподключение к серверу с повторными попытками
        /// </summary>
        public async Task<bool> ReconnectAsync()
        {
            for (int attempt = 1; attempt <= ServerConfig.MAX_RECONNECT_ATTEMPTS; attempt++)
            {
                Console.WriteLine($"🔄 Попытка переподключения {attempt}/{ServerConfig.MAX_RECONNECT_ATTEMPTS}...");
                
                if (await ConnectAsync())
                {
                    if (await RegisterAsRobotAsync())
                    {
                        Console.WriteLine("✅ Переподключение успешно");
                        return true;
                    }
                }
                
                if (attempt < ServerConfig.MAX_RECONNECT_ATTEMPTS)
                {
                    Console.WriteLine($"⏳ Ожидание {ServerConfig.RECONNECT_DELAY_SECONDS}s перед следующей попыткой...");
                    await Task.Delay(TimeSpan.FromSeconds(ServerConfig.RECONNECT_DELAY_SECONDS));
                }
            }
            
            Console.WriteLine("❌ Не удалось переподключиться к серверу");
            return false;
        }

        /// <summary>
        /// Отключение от сервера
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client disconnecting",
                        CancellationToken.None
                    );
                }
                
                Console.WriteLine("✅ WebSocket отключен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка при отключении: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
} 