using System.IO.Ports;
using System.Text.RegularExpressions;

namespace RobotClient.Control
{
    /// <summary>
    /// Класс для управления Pixhawk через USB в Linux с использованием официальной MAVLink библиотеки
    /// </summary>
    public class PixhawkControl : IDisposable
    {
        private SerialPort? _serialPort;
        private bool _isConnected = false;
        private readonly object _lockObject = new object();
        private byte _systemId = 255; // GCS system ID
        private byte _componentId = 0; // GCS component ID
        private byte _targetSystemId = 1; // Pixhawk system ID
        private byte _targetComponentId = 1; // Pixhawk component ID
        private MAVLink.MavlinkParse _mavlinkParser;
        private DateTime _lastHeartbeat = DateTime.MinValue;
        private ushort _sequenceNumber = 0;

        /// <summary>
        /// Информация о подключенном Pixhawk
        /// </summary>
        public class PixhawkInfo
        {
            public string PortName { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public int BaudRate { get; set; }
            public bool IsConnected { get; set; }
        }

        /// <summary>
        /// Текущая информация о подключении
        /// </summary>
        public PixhawkInfo? CurrentConnection { get; private set; }

        /// <summary>
        /// Событие получения данных от Pixhawk (для будущего использования)
        /// </summary>
        #pragma warning disable CS0067 // Событие никогда не используется
        public event Action<MAVLink.MAVLinkMessage>? MessageReceived;
        #pragma warning restore CS0067

        public PixhawkControl()
        {
            _mavlinkParser = new MAVLink.MavlinkParse();
        }

        /// <summary>
        /// Проверяет, подключен ли Pixhawk
        /// </summary>
        /// <returns>True если Pixhawk подключен и готов к работе</returns>
        public bool IsConnected()
        {
            lock (_lockObject)
            {
                if (!_isConnected || _serialPort == null || !_serialPort.IsOpen)
                {
                    return false;
                }

                // Проверяем, получали ли мы heartbeat в последние 5 секунд
                if (DateTime.UtcNow - _lastHeartbeat > TimeSpan.FromSeconds(5))
                {
                    Console.WriteLine("Потеря связи с Pixhawk (нет heartbeat > 5 сек)");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Ищет и подключается к Pixhawk
        /// </summary>
        /// <returns>True если подключение успешно</returns>
        public bool Connect()
        {
            Console.WriteLine("Поиск Pixhawk...");

            // Отключаемся от текущего соединения
            Disconnect();

            var pixhawkPorts = FindPixhawkPorts();
            
            if (pixhawkPorts.Count == 0)
            {
                Console.WriteLine("Pixhawk устройства не найдены");
                return false;
            }

            Console.WriteLine($"Найдено {pixhawkPorts.Count} потенциальных Pixhawk устройств");

            // Пробуем подключиться к каждому найденному порту
            foreach (var portInfo in pixhawkPorts)
            {
                if (TryConnectToPort(portInfo))
                {
                    CurrentConnection = portInfo;
                    Console.WriteLine($"Успешно подключен к Pixhawk на порту {portInfo.PortName}");
                    return true;
                }
            }

            Console.WriteLine("Не удалось подключиться ни к одному Pixhawk устройству");
            return false;
        }

        /// <summary>
        /// Отключается от Pixhawk
        /// </summary>
        public void Disconnect()
        {
            lock (_lockObject)
            {
                _isConnected = false;
                
                if (_serialPort != null)
                {
                    try
                    {
                        _serialPort.DataReceived -= OnDataReceived;
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при отключении: {ex.Message}");
                    }
                    finally
                    {
                        _serialPort = null;
                        CurrentConnection = null;
                    }
                }
            }
        }

        /// <summary>
        /// Устанавливает скорость моторов через PWM выходы
        /// </summary>
        /// <param name="leftWheelSpeed">Скорость левых колес (-1.0 до 1.0)</param>
        /// <param name="rightWheelSpeed">Скорость правых колес (-1.0 до 1.0)</param>
        /// <returns>True если команда отправлена успешно</returns>
        public bool SetWheelSpeeds(float leftWheelSpeed, float rightWheelSpeed)
        {
            if (!IsConnected())
            {
                Console.WriteLine("Pixhawk не подключен. Попытка переподключения...");
                if (!Connect())
                {
                    return false;
                }
            }

            try
            {
                // Ограничиваем значения скорости
                leftWheelSpeed = Math.Max(-1.0f, Math.Min(1.0f, leftWheelSpeed));
                rightWheelSpeed = Math.Max(-1.0f, Math.Min(1.0f, rightWheelSpeed));

                // Конвертируем скорость в PWM значения (1000-2000 мкс)
                ushort leftPwm = ConvertSpeedToPwm(leftWheelSpeed);
                ushort rightPwm = ConvertSpeedToPwm(rightWheelSpeed);

                Console.WriteLine($"Установка скорости: левые={leftWheelSpeed:F2} ({leftPwm}мкс), правые={rightWheelSpeed:F2} ({rightPwm}мкс)");

                // Отправляем RC Override команду
                return SendRcOverride(leftPwm, rightPwm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при установке скорости моторов: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Остановка всех моторов
        /// </summary>
        /// <returns>True если команда отправлена успешно</returns>
        public bool StopMotors()
        {
            return SetWheelSpeeds(0.0f, 0.0f);
        }

        /// <summary>
        /// Отправка Heartbeat сообщения
        /// </summary>
        public bool SendHeartbeat()
        {
            try
            {
                var heartbeat = new MAVLink.mavlink_heartbeat_t
                {
                    type = (byte)MAVLink.MAV_TYPE.GCS,
                    autopilot = (byte)MAVLink.MAV_AUTOPILOT.INVALID,
                    base_mode = 0,
                    custom_mode = 0,
                    system_status = (byte)MAVLink.MAV_STATE.ACTIVE,
                    mavlink_version = 3
                };

                return SendMessage(MAVLink.MAVLINK_MSG_ID.HEARTBEAT, heartbeat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки heartbeat: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Поиск портов с Pixhawk устройствами
        /// </summary>
        private List<PixhawkInfo> FindPixhawkPorts()
        {
            var pixhawkPorts = new List<PixhawkInfo>();

            try
            {
                // В Linux Pixhawk обычно появляется как /dev/ttyACM* или /dev/ttyUSB*
                var potentialPorts = new List<string>();
                
                // Ищем ACM порты (USB CDC)
                var acmPorts = Directory.GetFiles("/dev", "ttyACM*").OrderBy(x => x).ToList();
                potentialPorts.AddRange(acmPorts);

                // Ищем USB порты
                var usbPorts = Directory.GetFiles("/dev", "ttyUSB*").OrderBy(x => x).ToList();
                potentialPorts.AddRange(usbPorts);

                Console.WriteLine($"Найдено {potentialPorts.Count} последовательных портов: {string.Join(", ", potentialPorts.Select(Path.GetFileName))}");

                foreach (var port in potentialPorts)
                {
                    try
                    {
                        // Получаем информацию об устройстве через udev
                        var udevInfo = ExecuteCommand($"udevadm info --name={port}");
                        
                        if (IsLikelyPixhawk(udevInfo, port))
                        {
                            pixhawkPorts.Add(new PixhawkInfo
                            {
                                PortName = port,
                                Description = ExtractDeviceDescription(udevInfo),
                                BaudRate = 57600, // Стандартная скорость для Pixhawk
                                IsConnected = false
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при проверке порта {port}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при поиске портов: {ex.Message}");
            }

            return pixhawkPorts;
        }

        /// <summary>
        /// Выполнение команды в Linux
        /// </summary>
        private string ExecuteCommand(string command)
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Проверка, является ли устройство Pixhawk
        /// </summary>
        private bool IsLikelyPixhawk(string udevInfo, string portName)
        {
            var info = udevInfo.ToLower();
            
            // Pixhawk ключевые слова
            var pixhawkKeywords = new[] 
            { 
                "pixhawk", "px4", "ardupilot", "cube", "holybro",
                "3dr", "mro", "hex", "drotek", "matek"
            };

            // USB идентификаторы известных Pixhawk устройств
            var knownVendorIds = new[] { "26ac", "2dae", "1209" }; // 3DR, CubePilot, Generic

            // Проверяем на известные ключевые слова
            if (pixhawkKeywords.Any(keyword => info.Contains(keyword)))
            {
                return true;
            }

            // Проверяем известные USB ID
            foreach (var vendorId in knownVendorIds)
            {
                if (info.Contains($"id_vendor_id={vendorId}"))
                {
                    return true;
                }
            }

            // Если это ACM порт, то скорее всего это может быть Pixhawk
            if (portName.Contains("ttyACM"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Извлечение описания устройства
        /// </summary>
        private string ExtractDeviceDescription(string udevInfo)
        {
            // Пытаемся извлечь модель и производителя
            var modelMatch = Regex.Match(udevInfo, @"ID_MODEL=(.+)", RegexOptions.IgnoreCase);
            var vendorMatch = Regex.Match(udevInfo, @"ID_VENDOR=(.+)", RegexOptions.IgnoreCase);

            if (modelMatch.Success && vendorMatch.Success)
            {
                return $"{vendorMatch.Groups[1].Value} {modelMatch.Groups[1].Value}".Replace("_", " ");
            }
            else if (modelMatch.Success)
            {
                return modelMatch.Groups[1].Value.Replace("_", " ");
            }

            return "USB Serial Device";
        }

        /// <summary>
        /// Попытка подключения к конкретному порту
        /// </summary>
        private bool TryConnectToPort(PixhawkInfo portInfo)
        {
            try
            {
                Console.WriteLine($"Попытка подключения к {portInfo.PortName}...");

                _serialPort = new SerialPort(portInfo.PortName, portInfo.BaudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };

                _serialPort.Open();
                _serialPort.DataReceived += OnDataReceived;

                // Ждем немного для стабилизации соединения
                Thread.Sleep(500);

                // Отправляем heartbeat и ждем ответ
                if (TestConnection())
                {
                    _isConnected = true;
                    portInfo.IsConnected = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения к {portInfo.PortName}: {ex.Message}");
            }

            // Закрываем порт при неудаче
            try
            {
                if (_serialPort != null)
                {
                    _serialPort.DataReceived -= OnDataReceived;
                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Тестирование соединения
        /// </summary>
        private bool TestConnection()
        {
            try
            {
                // Отправляем heartbeat
                SendHeartbeat();
                
                // Ждем ответ от Pixhawk (упрощенная проверка)
                Thread.Sleep(1000);
                
                return _serialPort != null && _serialPort.IsOpen;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Обработчик входящих данных
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                var buffer = new byte[_serialPort.BytesToRead];
                int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);

                // Упрощенная обработка - просто отмечаем, что получили данные
                if (bytesRead > 0)
                {
                    _lastHeartbeat = DateTime.UtcNow;
                    Console.WriteLine($"Получены данные от Pixhawk: {bytesRead} байт");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки входящих данных: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка RC Override команды
        /// </summary>
        private bool SendRcOverride(ushort leftPwm, ushort rightPwm)
        {
            try
            {
                var rcOverride = new MAVLink.mavlink_rc_channels_override_t
                {
                    target_system = _targetSystemId,
                    target_component = _targetComponentId,
                    chan1_raw = leftPwm,   // Левые колеса
                    chan2_raw = rightPwm,  // Правые колеса
                    chan3_raw = 0,         // Не используется
                    chan4_raw = 0,         // Не используется
                    chan5_raw = 0,         // Не используется
                    chan6_raw = 0,         // Не используется
                    chan7_raw = 0,         // Не используется
                    chan8_raw = 0          // Не используется
                };

                return SendMessage(MAVLink.MAVLINK_MSG_ID.RC_CHANNELS_OVERRIDE, rcOverride);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки RC Override: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отправка MAVLink сообщения
        /// </summary>
        private bool SendMessage(MAVLink.MAVLINK_MSG_ID msgId, object messageData)
        {
            try
            {
                lock (_lockObject)
                {
                    if (_serialPort == null || !_serialPort.IsOpen) return false;

                    // Сериализуем сообщение
                    var bytes = _mavlinkParser.GenerateMAVLinkPacket20(msgId, messageData, false, _systemId, _componentId, _sequenceNumber++);

                    // Отправляем данные
                    _serialPort.Write(bytes, 0, bytes.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки сообщения: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Конвертация скорости (-1..1) в PWM значение (1000-2000)
        /// </summary>
        private ushort ConvertSpeedToPwm(float speed)
        {
            // Ограничиваем скорость
            speed = Math.Max(-1.0f, Math.Min(1.0f, speed));
            
            // Конвертируем в PWM: 1500 - центр, 1000-2000 - диапазон
            var pwm = 1500 + (int)(speed * 500);
            return (ushort)Math.Max(1000, Math.Min(2000, pwm));
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
} 