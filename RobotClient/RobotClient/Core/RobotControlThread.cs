using RobotClient.Control;
using RobotClient.Config;
using RobotClient.Video;

namespace RobotClient.Core
{
    /// <summary>
    /// Поток управления роботом - обработка команд, телеметрия, управление движением
    /// </summary>
    public class RobotControlThread : IDisposable
    {
        private readonly WebSocketClient _webSocketService;
        private readonly CommandProcessing _commandProcessingService;
        private readonly PixhawkControl _pixhawkControl;
        
        private bool _isRunning;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _controlTask;
        private Timer? _telemetryTimer;

        public bool IsRunning => _isRunning;
        public bool IsPixhawkConnected => _pixhawkControl.IsConnected();

        public RobotControlThread(WebSocketClient webSocketService)
        {
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _pixhawkControl = new PixhawkControl();
            _commandProcessingService = new CommandProcessing(_pixhawkControl, _webSocketService);
            
            Console.WriteLine("🎮 Поток управления роботом инициализирован");
        }

        /// <summary>
        /// Запуск потока управления роботом
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                if (_isRunning)
                {
                    Console.WriteLine("⚠️ Поток управления роботом уже запущен");
                    return true;
                }

                Console.WriteLine("🚀 Запуск потока управления роботом...");

                // 1. Подключение к Pixhawk
                Console.WriteLine("📡 Подключение к Pixhawk...");
                if (!_pixhawkControl.IsConnected())
                {
                    if (!_pixhawkControl.Connect())
                    {
                        Console.WriteLine("❌ Не удалось подключиться к Pixhawk");
                        return false;
                    }
                }
                Console.WriteLine("✅ Pixhawk подключен");

                // 2. Запуск обработки команд
                Console.WriteLine("🎮 Запуск обработки команд...");
                _commandProcessingService.StartProcessing();
                Console.WriteLine("✅ Обработка команд запущена");

                // 3. Запуск фонового потока управления
                _cancellationTokenSource = new CancellationTokenSource();
                _controlTask = Task.Run(() => ControlLoopAsync(_cancellationTokenSource.Token));

                // 4. Запуск таймера телеметрии
                _telemetryTimer = new Timer(async _ => await SendTelemetryAsync(), 
                                          null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

                _isRunning = true;
                Console.WriteLine("🟢 Поток управления роботом запущен успешно!");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска потока управления роботом: {ex.Message}");
                await StopAsync();
                return false;
            }
        }

        /// <summary>
        /// Остановка потока управления роботом
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (!_isRunning)
                    return;

                Console.WriteLine("🛑 Остановка потока управления роботом...");
                _isRunning = false;

                // Остановка таймера телеметрии
                _telemetryTimer?.Dispose();
                _telemetryTimer = null;

                // Остановка фонового потока
                _cancellationTokenSource?.Cancel();
                
                if (_controlTask != null)
                {
                    try
                    {
                        await _controlTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // Ожидаемое исключение при отмене
                    }
                }

                // Остановка обработки команд
                _commandProcessingService.StopProcessing();
                Console.WriteLine("✅ Обработка команд остановлена");

                // Остановка моторов и отключение Pixhawk
                if (_pixhawkControl.IsConnected())
                {
                    _pixhawkControl.StopMotors();
                    _pixhawkControl.Dispose();
                    Console.WriteLine("✅ Pixhawk отключен");
                }

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _controlTask = null;

                Console.WriteLine("🔴 Поток управления роботом остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка остановки потока управления: {ex.Message}");
            }
        }

        /// <summary>
        /// Главный цикл управления роботом
        /// </summary>
        private async Task ControlLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("🔄 Запущен главный цикл управления роботом");

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    // Проверка состояния подключений
                    if (!_webSocketService.IsConnected)
                    {
                        Console.WriteLine("⚠️ WebSocket отключен, ожидание переподключения...");
                        await Task.Delay(5000, cancellationToken);
                        continue;
                    }

                    if (!_pixhawkControl.IsConnected())
                    {
                        Console.WriteLine("⚠️ Pixhawk отключен, попытка переподключения...");
                        _pixhawkControl.Connect();
                        await Task.Delay(5000, cancellationToken);
                        continue;
                    }

                    // Основной цикл управления (можно добавить дополнительную логику)
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🛑 Главный цикл управления остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в главном цикле управления: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка телеметрии статуса управления
        /// </summary>
        private async Task SendTelemetryAsync()
        {
            try
            {
                if (!_webSocketService.IsRegistered || !_isRunning)
                    return;

                var controlStatus = new
                {
                    type = "robot_control_status",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    control = new
                    {
                        pixhawk = new
                        {
                            connected = _pixhawkControl.IsConnected(),
                            connectionInfo = _pixhawkControl.CurrentConnection?.ToString()
                        },
                        commandProcessing = new
                        {
                            active = _commandProcessingService.IsProcessing
                        },
                        thread = new
                        {
                            running = _isRunning,
                            threadId = Environment.CurrentManagedThreadId
                        }
                    }
                };

                await _webSocketService.SendTelemetryAsync(controlStatus);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки телеметрии управления: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение статуса потока управления
        /// </summary>
        public object GetStatus()
        {
            return new
            {
                isRunning = _isRunning,
                pixhawk = new
                {
                    connected = _pixhawkControl.IsConnected(),
                    connectionInfo = _pixhawkControl.CurrentConnection?.ToString()
                },
                commandProcessing = new
                {
                    active = _commandProcessingService.IsProcessing
                }
            };
        }

        public void Dispose()
        {
            _ = StopAsync();
            _telemetryTimer?.Dispose();
            _cancellationTokenSource?.Dispose();
            _commandProcessingService?.Dispose();
            _pixhawkControl?.Dispose();
        }
    }
} 