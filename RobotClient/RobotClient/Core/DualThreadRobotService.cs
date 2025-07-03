using RobotClient.Config;
using RobotClient.Video;
using RobotClient.Control;

namespace RobotClient.Core
{
    /// <summary>
    /// Главный сервис робота с двумя независимыми потоками:
    /// 1. Поток управления роботом (команды, телеметрия, Pixhawk)
    /// 2. Поток трансляции видео (камера, WebRTC, видео сигналинг)
    /// </summary>
    public class DualThreadRobotService : IDisposable
    {
        private readonly WebSocketClient _webSocketService;
        private readonly RobotControlThread _controlThread;
        private readonly VideoStreamingThread _videoThread;
        
        private bool _isRunning;
        private readonly string _serverUrl;

        public bool IsRunning => _isRunning;
        public bool IsConnected => _webSocketService.IsConnected;
        public bool IsRegistered => _webSocketService.IsRegistered;
        
        // Статусы потоков
        public bool IsControlThreadRunning => _controlThread.IsRunning;
        public bool IsVideoThreadRunning => _videoThread.IsRunning;
        public bool IsPixhawkConnected => _controlThread.IsPixhawkConnected;
        public bool IsVideoInitialized => _videoThread.IsVideoInitialized;

        public DualThreadRobotService(string? serverUrl = null)
        {
            _serverUrl = serverUrl ?? ServerConfig.WebSocketUrl;
            
            // Инициализация WebSocket сервиса
            _webSocketService = new WebSocketClient(_serverUrl);
            
            // Инициализация потоков
            _controlThread = new RobotControlThread(_webSocketService);
            _videoThread = new VideoStreamingThread(_webSocketService);
            
            Console.WriteLine("🤖 Двухпоточный сервис робота инициализирован");
            Console.WriteLine($"📡 Сервер: {_serverUrl}");
        }

        /// <summary>
        /// Запуск двухпоточного сервиса робота
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                if (_isRunning)
                {
                    Console.WriteLine("⚠️ Двухпоточный сервис робота уже запущен");
                    return true;
                }

                Console.WriteLine("🚀 Запуск двухпоточного сервиса робота...");

                // ЭТАП 1: Подключение к WebSocket серверу
                Console.WriteLine(new string('=', 50));
                Console.WriteLine("📡 ЭТАП 1: Подключение к WebSocket серверу");
                Console.WriteLine(new string('=', 50));

                Console.WriteLine($"🔌 Подключение к серверу {_serverUrl}...");
                if (!await _webSocketService.ConnectAsync())
                {
                    Console.WriteLine("❌ Не удалось подключиться к серверу");
                    return false;
                }
                Console.WriteLine("✅ Подключение к серверу установлено");

                Console.WriteLine("📝 Регистрация робота...");
                if (!await _webSocketService.RegisterAsRobotAsync())
                {
                    Console.WriteLine("❌ Не удалось зарегистрировать робота");
                    await _webSocketService.DisconnectAsync();
                    return false;
                }
                Console.WriteLine("✅ Робот зарегистрирован на сервере");

                // ЭТАП 2: Запуск двух независимых потоков
                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("🔄 ЭТАП 2: Запуск двух жизненных циклов (потоков)");
                Console.WriteLine(new string('=', 50));

                // Запускаем оба потока параллельно
                var controlTask = StartControlThreadAsync();
                var videoTask = StartVideoThreadAsync();

                // Ждем завершения запуска обоих потоков
                var results = await Task.WhenAll(controlTask, videoTask);
                
                bool controlStarted = results[0];
                bool videoStarted = results[1];

                Console.WriteLine("\n" + new string('=', 50));
                Console.WriteLine("📊 РЕЗУЛЬТАТЫ ЗАПУСКА:");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine($"🎮 Поток управления роботом: {(controlStarted ? "✅ ЗАПУЩЕН" : "❌ ОШИБКА")}");
                Console.WriteLine($"📹 Поток трансляции видео: {(videoStarted ? "✅ ЗАПУЩЕН" : "❌ ОШИБКА")}");

                if (controlStarted || videoStarted)
                {
                    _isRunning = true;
                    Console.WriteLine("\n🟢 Двухпоточный сервис робота запущен!");
                    Console.WriteLine("🔄 Работают независимые жизненные циклы:");
                    if (controlStarted) Console.WriteLine("   🎮 Управление роботом");
                    if (videoStarted) Console.WriteLine("   📹 Трансляция видео");
                    return true;
                }
                else
                {
                    Console.WriteLine("\n❌ Не удалось запустить ни один поток");
                    await StopAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска двухпоточного сервиса: {ex.Message}");
                await StopAsync();
                return false;
            }
        }

        /// <summary>
        /// Запуск потока управления роботом
        /// </summary>
        private async Task<bool> StartControlThreadAsync()
        {
            try
            {
                Console.WriteLine("🎮 Запуск потока управления роботом...");
                bool result = await _controlThread.StartAsync();
                
                if (result)
                {
                    Console.WriteLine("✅ Поток управления роботом запущен успешно");
                }
                else
                {
                    Console.WriteLine("❌ Не удалось запустить поток управления роботом");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска потока управления: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Запуск потока трансляции видео
        /// </summary>
        private async Task<bool> StartVideoThreadAsync()
        {
            try
            {
                Console.WriteLine("📹 Запуск потока трансляции видео...");
                bool result = await _videoThread.StartAsync();
                
                if (result)
                {
                    Console.WriteLine("✅ Поток трансляции видео запущен успешно");
                }
                else
                {
                    Console.WriteLine("❌ Не удалось запустить поток трансляции видео");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска потока видео: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Остановка двухпоточного сервиса робота
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (!_isRunning)
                    return;

                Console.WriteLine("🛑 Остановка двухпоточного сервиса робота...");
                _isRunning = false;

                // Остановка обоих потоков параллельно
                var controlStopTask = _controlThread.StopAsync();
                var videoStopTask = _videoThread.StopAsync();

                await Task.WhenAll(controlStopTask, videoStopTask);

                // Отключение от WebSocket сервера
                await _webSocketService.DisconnectAsync();
                Console.WriteLine("✅ WebSocket отключен");

                Console.WriteLine("🔴 Двухпоточный сервис робота остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка остановки двухпоточного сервиса: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение полного статуса двухпоточного сервиса
        /// </summary>
        public object GetStatus()
        {
            return new
            {
                service = new
                {
                    isRunning = _isRunning,
                    serverUrl = _serverUrl
                },
                webSocket = new
                {
                    connected = _webSocketService.IsConnected,
                    registered = _webSocketService.IsRegistered
                },
                controlThread = _controlThread.GetStatus(),
                videoThread = _videoThread.GetStatus(),
                summary = new
                {
                    threadsRunning = new
                    {
                        control = _controlThread.IsRunning,
                        video = _videoThread.IsRunning
                    },
                    capabilities = new
                    {
                        robotControl = _controlThread.IsRunning && _controlThread.IsPixhawkConnected,
                        videoStreaming = _videoThread.IsRunning && _videoThread.IsVideoInitialized
                    }
                }
            };
        }

        /// <summary>
        /// Отправка полной телеметрии статуса
        /// </summary>
        public async Task SendStatusTelemetryAsync()
        {
            try
            {
                if (!_webSocketService.IsRegistered || !_isRunning)
                    return;

                var status = new
                {
                    type = "robot_dual_thread_status",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    robotStatus = GetStatus()
                };

                await _webSocketService.SendTelemetryAsync(status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки телеметрии двухпоточного статуса: {ex.Message}");
            }
        }

        /// <summary>
        /// Запуск в фоновом режиме с мониторингом потоков
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Запускаем двухпоточный сервис
                if (!await StartAsync())
                {
                    Console.WriteLine("❌ Не удалось запустить двухпоточный сервис робота");
                    return;
                }

                Console.WriteLine("🔄 Двухпоточный робот работает. Нажмите Ctrl+C для остановки...");

                // Периодическая отправка телеметрии и мониторинг
                var telemetryTimer = new Timer(async _ =>
                {
                    await SendStatusTelemetryAsync();
                    MonitorThreads();
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(45));

                // Ожидаем сигнала остановки
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("🛑 Получен сигнал остановки двухпоточного сервиса");
                }

                telemetryTimer.Dispose();
            }
            finally
            {
                await StopAsync();
            }
        }

        /// <summary>
        /// Мониторинг состояния потоков
        /// </summary>
        private void MonitorThreads()
        {
            try
            {
                if (!_isRunning)
                    return;

                var controlRunning = _controlThread.IsRunning;
                var videoRunning = _videoThread.IsRunning;
                var pixhawkConnected = _controlThread.IsPixhawkConnected;
                var videoInitialized = _videoThread.IsVideoInitialized;

                Console.WriteLine($"📊 Статус потоков: 🎮 Control:{(controlRunning ? "✅" : "❌")} | " +
                               $"📹 Video:{(videoRunning ? "✅" : "❌")} | " +
                               $"🔧 Pixhawk:{(pixhawkConnected ? "✅" : "❌")} | " +
                               $"📷 Camera:{(videoInitialized ? "✅" : "❌")}");

                // Можно добавить логику перезапуска потоков при необходимости
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка мониторинга потоков: {ex.Message}");
            }
        }

        /// <summary>
        /// Принудительная переинициализация видео сервиса
        /// </summary>
        public async Task<bool> ReinitializeVideoAsync()
        {
            return await _videoThread.ReinitializeVideoAsync();
        }

        public void Dispose()
        {
            _ = StopAsync();
            _webSocketService?.Dispose();
            _controlThread?.Dispose();
            _videoThread?.Dispose();
        }
    }
} 