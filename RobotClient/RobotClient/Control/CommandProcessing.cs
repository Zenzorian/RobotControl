using System.Text.Json;
using RobotClient.Core;

namespace RobotClient.Control
{
    /// <summary>
    /// Сервис для обработки команд управления роботом
    /// </summary>
    public class CommandProcessing
    {
        private readonly PixhawkControl _pixhawkControl;
        private readonly WebSocketClient _webSocketService;
        private bool _isProcessing;

        public event Action<string>? CommandReceived;
        public event Action<string>? CommandProcessed;
        public event Action<string>? ProcessingError;

        public bool IsProcessing => _isProcessing;

        public CommandProcessing(PixhawkControl pixhawkControl, WebSocketClient webSocketService)
        {
            _pixhawkControl = pixhawkControl ?? throw new ArgumentNullException(nameof(pixhawkControl));
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            
            // Подписываемся на сообщения от WebSocket
            _webSocketService.MessageReceived += OnMessageReceived;
            
            Console.WriteLine("🎮 Сервис обработки команд инициализирован");
        }

        /// <summary>
        /// Запуск обработки команд
        /// </summary>
        public void StartProcessing()
        {
            if (_isProcessing)
            {
                Console.WriteLine("⚠️ Обработка команд уже запущена");
                return;
            }

            _isProcessing = true;
            Console.WriteLine("▶️ Запущена обработка команд управления");
        }

        /// <summary>
        /// Остановка обработки команд
        /// </summary>
        public void StopProcessing()
        {
            _isProcessing = false;
            
            // Аварийная остановка моторов
            try
            {
                _pixhawkControl.StopMotors();
                Console.WriteLine("🛑 Моторы остановлены (аварийная остановка)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка остановки моторов: {ex.Message}");
            }
            
            Console.WriteLine("⏹️ Обработка команд остановлена");
        }

        /// <summary>
        /// Обработка входящих сообщений
        /// </summary>
        private async void OnMessageReceived(string message)
        {
            if (!_isProcessing)
                return;

            try
            {
                // Проверяем тип сообщения
                if (message.StartsWith("COMMAND!"))
                {
                    await ProcessControlCommand(message);
                }
                else if (message.StartsWith("ERROR!"))
                {
                    ProcessErrorMessage(message);
                }
                else if (message == "SERVER_SHUTDOWN")
                {
                    ProcessServerShutdown();
                }
                // Игнорируем другие типы сообщений (они обрабатываются другими сервисами)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки сообщения: {ex.Message}");
                ProcessingError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Обработка команды управления
        /// </summary>
        private async Task ProcessControlCommand(string message)
        {
            try
            {
                CommandReceived?.Invoke(message);
                
                // Парсим команду с помощью CommandConverter
                var command = CommandConverter.ParseCommand(message);
                
                if (!command.HasValue)
                {
                    Console.WriteLine("❌ Не удалось распарсить команду");
                    return;
                }

                var cmd = command.Value;
                Console.WriteLine($"🎮 Обработка команды: L({cmd.leftStickValue.x:F2},{cmd.leftStickValue.y:F2}) R({cmd.rightStickValue.x:F2},{cmd.rightStickValue.y:F2})");

                // Проверяем подключение Pixhawk
                if (!_pixhawkControl.IsConnected())
                {
                    Console.WriteLine("❌ Pixhawk не подключен");
                    await SendStatusUpdate("PIXHAWK_DISCONNECTED");
                    return;
                }

                // Обрабатываем команды движения
                await ProcessMovementCommand(cmd);

                // Обрабатываем команды камеры
                await ProcessCameraCommand(cmd.cameraAngle);

                CommandProcessed?.Invoke(message);
                
                // Отправляем подтверждение обработки команды
                await SendCommandAck(cmd);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки команды: {ex.Message}");
                ProcessingError?.Invoke(ex.Message);
                await SendStatusUpdate($"COMMAND_ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка команд движения
        /// </summary>
        private async Task ProcessMovementCommand(Command command)
        {
            try
            {
                // Конвертируем команды стиков в скорости колес
                var (leftSpeed, rightSpeed) = ConvertSticksToWheelSpeeds(
                    command.leftStickValue, 
                    command.rightStickValue
                );

                // Ограничиваем скорости
                leftSpeed = Math.Clamp(leftSpeed, -1.0f, 1.0f);
                rightSpeed = Math.Clamp(rightSpeed, -1.0f, 1.0f);

                Console.WriteLine($"🔄 Установка скоростей колес: L={leftSpeed:F2}, R={rightSpeed:F2}");
                
                // Отправляем команду на Pixhawk
                _pixhawkControl.SetWheelSpeeds(leftSpeed, rightSpeed);

                // Отправляем телеметрию движения
                await SendMovementTelemetry(leftSpeed, rightSpeed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка команды движения: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Конвертация команд стиков в скорости колес
        /// </summary>
        private (float leftSpeed, float rightSpeed) ConvertSticksToWheelSpeeds(
            Vector2 leftStick, 
            Vector2 rightStick)
        {
            // Базовая скорость от левого стика (вперед/назад)
            float forwardSpeed = leftStick.y;
            
            // Поворот от правого стика (или левого стика по X)
            float turnSpeed = rightStick.x != 0 ? rightStick.x : leftStick.x;

            // Дифференциальное управление
            float leftSpeed = forwardSpeed - turnSpeed;
            float rightSpeed = forwardSpeed + turnSpeed;

            return (leftSpeed, rightSpeed);
        }

        /// <summary>
        /// Обработка команд камеры
        /// </summary>
        private async Task ProcessCameraCommand(float angle)
        {
            try
            {
                Console.WriteLine($"📷 Команда камеры: поворот на {angle}°");
                
                // TODO: Реализовать управление поворотом камеры
                // Здесь можно добавить управление сервоприводом камеры
                
                await SendCameraTelemetry(angle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка команды камеры: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Обработка сообщений об ошибках
        /// </summary>
        private void ProcessErrorMessage(string message)
        {
            Console.WriteLine($"⚠️ Ошибка от сервера: {message}");
            
            if (message.Contains("TARGET_DISCONNECTED"))
            {
                Console.WriteLine("🔌 Контроллер отключился - останавливаем моторы");
                _pixhawkControl.StopMotors();
            }
        }

        /// <summary>
        /// Обработка отключения сервера
        /// </summary>
        private void ProcessServerShutdown()
        {
            Console.WriteLine("🛑 Сервер отключается - останавливаем все операции");
            StopProcessing();
        }

        /// <summary>
        /// Отправка подтверждения обработки команды
        /// </summary>
        private async Task SendCommandAck(Command command)
        {
            try
            {
                var ackData = new
                {
                    type = "command_ack",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    command = new
                    {
                        leftStick = command.leftStickValue,
                        rightStick = command.rightStickValue,
                        cameraAngle = command.cameraAngle
                    }
                };

                await _webSocketService.SendTelemetryAsync(ackData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки подтверждения: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка телеметрии движения
        /// </summary>
        private async Task SendMovementTelemetry(float leftSpeed, float rightSpeed)
        {
            try
            {
                var telemetry = new
                {
                    type = "movement_telemetry",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    wheelSpeeds = new { left = leftSpeed, right = rightSpeed },
                    pixhawkConnected = _pixhawkControl.IsConnected()
                };

                await _webSocketService.SendTelemetryAsync(telemetry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки телеметрии: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка телеметрии камеры
        /// </summary>
        private async Task SendCameraTelemetry(float angle)
        {
            try
            {
                var telemetry = new
                {
                    type = "camera_telemetry",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    cameraAngle = angle
                };

                await _webSocketService.SendTelemetryAsync(telemetry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки телеметрии камеры: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка обновления статуса
        /// </summary>
        private async Task SendStatusUpdate(string status)
        {
            try
            {
                var statusData = new
                {
                    type = "status_update",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    status = status,
                    pixhawkConnected = _pixhawkControl.IsConnected(),
                    processingActive = _isProcessing
                };

                await _webSocketService.SendTelemetryAsync(statusData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка отправки статуса: {ex.Message}");
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            StopProcessing();
            _webSocketService.MessageReceived -= OnMessageReceived;
        }
    }
} 