using RobotClient.Config;
using RobotClient.Video;

namespace RobotClient.Core
{
    /// <summary>
    /// Поток трансляции видео - управление камерой, WebRTC сигналинг, видео потоки
    /// </summary>
    public class VideoStreamingThread : IDisposable
    {
        private readonly WebSocketClient _webSocketService;
        private readonly WebRTC _webRTCService;
        private readonly VideoStreaming _videoStreamingService;
        
        private bool _isRunning;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _streamingTask;
        private Timer? _statusTimer;

        public bool IsRunning => _isRunning;
        public bool IsVideoInitialized { get; private set; }
        public bool IsWebRTCActive => _webRTCService.IsActive;
        public int ActiveSessions => _webRTCService.ActiveSessionsCount;

        public VideoStreamingThread(WebSocketClient webSocketService)
        {
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _videoStreamingService = new VideoStreaming();
            _webRTCService = new WebRTC(_webSocketService, _videoStreamingService);
            
            Console.WriteLine("📹 Поток трансляции видео инициализирован");
        }

        /// <summary>
        /// Запуск потока трансляции видео
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                if (_isRunning)
                {
                    Console.WriteLine("⚠️ Поток трансляции видео уже запущен");
                    return true;
                }

                Console.WriteLine("🚀 Запуск потока трансляции видео...");

                // 1. Инициализация видео сервиса
                Console.WriteLine("📹 Инициализация видео сервиса...");
                IsVideoInitialized = await _videoStreamingService.InitializeAsync();
                if (!IsVideoInitialized)
                {
                    Console.WriteLine("⚠️ Видео сервис не инициализирован (камера не найдена), но поток будет запущен");
                }
                else
                {
                    Console.WriteLine("✅ Видео сервис инициализирован");
                }

                // 2. Запуск WebRTC сервиса
                Console.WriteLine("🎥 Запуск WebRTC сервиса...");
                if (!await _webRTCService.StartAsync())
                {
                    Console.WriteLine("⚠️ WebRTC сервис не запущен, но поток будет продолжать работать");
                }
                else
                {
                    Console.WriteLine($"✅ WebRTC сервис запущен, активен: {_webRTCService.IsActive}");
                }

                // 3. Запуск фонового потока видео
                _cancellationTokenSource = new CancellationTokenSource();
                _streamingTask = Task.Run(() => StreamingLoopAsync(_cancellationTokenSource.Token));

                // 4. Запуск таймера статуса видео
                _statusTimer = new Timer(async _ => await SendVideoStatusAsync(), 
                                       null, TimeSpan.Zero, TimeSpan.FromSeconds(60));

                _isRunning = true;
                Console.WriteLine("🟢 Поток трансляции видео запущен успешно!");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска потока трансляции видео: {ex.Message}");
                await StopAsync();
                return false;
            }
        }

        /// <summary>
        /// Остановка потока трансляции видео
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (!_isRunning)
                    return;

                Console.WriteLine("🛑 Остановка потока трансляции видео...");
                _isRunning = false;

                // Остановка таймера статуса
                _statusTimer?.Dispose();
                _statusTimer = null;

                // Остановка фонового потока
                _cancellationTokenSource?.Cancel();
                
                if (_streamingTask != null)
                {
                    try
                    {
                        await _streamingTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Ожидаемое исключение при отмене
                    }
                }

                // Остановка WebRTC сервиса
                await _webRTCService.StopAsync();
                Console.WriteLine("✅ WebRTC сервис остановлен");

                // Остановка видео сервиса
                await _videoStreamingService.StopAsync();
                Console.WriteLine("✅ Видео сервис остановлен");

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _streamingTask = null;
                IsVideoInitialized = false;

                Console.WriteLine("🔴 Поток трансляции видео остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка остановки потока трансляции видео: {ex.Message}");
            }
        }

        /// <summary>
        /// Главный цикл трансляции видео
        /// </summary>
        private async Task StreamingLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("🔄 Запущен главный цикл трансляции видео");

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    // Проверка состояния WebSocket подключения
                    if (!_webSocketService.IsConnected)
                    {
                        Console.WriteLine("⚠️ WebSocket отключен, видео поток в режиме ожидания...");
                        await Task.Delay(10000, cancellationToken);
                        continue;
                    }

                    // Проверка и переинициализация видео сервиса при необходимости
                    if (!IsVideoInitialized)
                    {
                        Console.WriteLine("🔄 Попытка переинициализации видео сервиса...");
                        IsVideoInitialized = await _videoStreamingService.InitializeAsync();
                        
                        if (IsVideoInitialized)
                        {
                            Console.WriteLine("✅ Видео сервис переинициализирован");
                        }
                        else
                        {
                            await Task.Delay(30000, cancellationToken); // Ждем дольше при неудаче
                            continue;
                        }
                    }

                    // Проверка активности WebRTC сессий
                    if (_webRTCService.IsActive && _webRTCService.ActiveSessionsCount > 0)
                    {
                        // Видео активно транслируется
                        Console.WriteLine($"🔄 WebRTC активен, сессий: {_webRTCService.ActiveSessionsCount}");
                        await Task.Delay(5000, cancellationToken);
                    }
                    else
                    {
                        // Нет активных сессий, ждем дольше
                        Console.WriteLine($"💤 WebRTC: активен={_webRTCService.IsActive}, сессий={_webRTCService.ActiveSessionsCount}");
                        await Task.Delay(10000, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🛑 Главный цикл трансляции видео остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в главном цикле трансляции видео: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка статуса видео трансляции
        /// </summary>
        private async Task SendVideoStatusAsync()
        {
            try
            {
                if (!_webSocketService.IsRegistered || !_isRunning)
                    return;

                var videoStatus = new
                {
                    type = "robot_video_status",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    video = new
                    {
                        initialized = IsVideoInitialized,
                        webRTC = new
                        {
                            active = _webRTCService.IsActive,
                            activeSessions = _webRTCService.ActiveSessionsCount
                        },
                        streaming = new
                        {
                            available = IsVideoInitialized && _webRTCService.IsActive
                        },
                        thread = new
                        {
                            running = _isRunning,
                            threadId = Environment.CurrentManagedThreadId
                        }
                    }
                };

                await _webSocketService.SendTelemetryAsync(videoStatus);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки статуса видео: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение статуса потока видео
        /// </summary>
        public object GetStatus()
        {
            return new
            {
                isRunning = _isRunning,
                videoInitialized = IsVideoInitialized,
                webRTC = new
                {
                    active = _webRTCService.IsActive,
                    activeSessions = _webRTCService.ActiveSessionsCount
                },
                streaming = new
                {
                    available = IsVideoInitialized && _webRTCService.IsActive
                }
            };
        }

        /// <summary>
        /// Принудительная переинициализация видео сервиса
        /// </summary>
        public async Task<bool> ReinitializeVideoAsync()
        {
            try
            {
                Console.WriteLine("🔄 Принудительная переинициализация видео сервиса...");
                
                // Остановка текущего видео сервиса
                await _videoStreamingService.StopAsync();
                IsVideoInitialized = false;

                // Пауза перед переинициализацией
                await Task.Delay(2000);

                // Повторная инициализация
                IsVideoInitialized = await _videoStreamingService.InitializeAsync();
                
                if (IsVideoInitialized)
                {
                    Console.WriteLine("✅ Видео сервис успешно переинициализирован");
                    return true;
                }
                else
                {
                    Console.WriteLine("❌ Не удалось переинициализировать видео сервис");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка переинициализации видео сервиса: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _ = StopAsync();
            _statusTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
            _webRTCService?.Dispose();
        }
    }
} 