using System.Text.RegularExpressions;

namespace RobotClient.Video
{
    /// <summary>
    /// Класс для поиска и получения информации о веб-камерах, подключенных по USB в Linux
    /// </summary>
    public class CameraSearch
    {
        /// <summary>
        /// Информация о найденной веб-камере
        /// </summary>
        public class CameraInfo
        {
            public string Name { get; set; } = string.Empty;
            public string DeviceID { get; set; } = string.Empty;
            public string PnpDeviceID { get; set; } = string.Empty;
            public bool IsConnected { get; set; }
            public string Description { get; set; } = string.Empty;
            public string DevicePath { get; set; } = string.Empty; // /dev/video0, etc.
            public string Capabilities { get; set; } = string.Empty;
            public bool SupportsVideoCapture { get; set; }
            public bool SupportsStreaming { get; set; }
            public List<string> SupportedFormats { get; set; } = new List<string>();
            public string DeviceType { get; set; } = string.Empty; // Camera, Metadata, etc.
        }

        /// <summary>
        /// Ищет и возвращает список всех подключенных USB веб-камер
        /// </summary>
        /// <returns>Список найденных веб-камер</returns>
        public List<CameraInfo> FindUsbWebCameras()
        {
            var cameras = new List<CameraInfo>();

            try
            {
                // Проверяем наличие v4l2-ctl
                bool hasV4l2Utils = CheckV4l2Utils();
                if (!hasV4l2Utils)
                {
                    Console.WriteLine("⚠️  v4l2-ctl не найден! Установите: sudo apt install v4l-utils");
                    Console.WriteLine("🔧 Используется fallback режим без анализа возможностей...");
                    Console.WriteLine();
                    return FindBasicCameras();
                }

                // Поиск видеоустройств в /dev/video*
                var videoDevices = Directory.GetFiles("/dev", "video*")
                    .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^video\d+$"))
                    .OrderBy(f => f)
                    .ToList();

                Console.WriteLine($"🔍 Найдено {videoDevices.Count} видеоустройств: {string.Join(", ", videoDevices.Select(Path.GetFileName))}");
                Console.WriteLine(new string('=', 80));

                foreach (var devicePath in videoDevices)
                {
                    try
                    {
                        var deviceInfo = GetDeviceInfo(devicePath);
                        if (deviceInfo != null)
                        {
                            Console.WriteLine($"📹 Устройство {devicePath}:");
                            Console.WriteLine($"   📛 Название: {deviceInfo.Name}");
                            Console.WriteLine($"   🏷️  Тип: {deviceInfo.DeviceType}");
                            Console.WriteLine($"   📺 Захват видео: {(deviceInfo.SupportsVideoCapture ? "✅" : "❌")}");
                            Console.WriteLine($"   🌊 Потоковое видео: {(deviceInfo.SupportsStreaming ? "✅" : "❌")}");
                            Console.WriteLine($"   🎨 Форматы: {(deviceInfo.SupportedFormats.Count > 0 ? string.Join(", ", deviceInfo.SupportedFormats) : "❌ Нет")}");
                            Console.WriteLine($"   ⚙️  Возможности: {deviceInfo.Capabilities}");
                            Console.WriteLine();

                            // Добавляем только устройства, поддерживающие захват видео
                            if (deviceInfo.SupportsVideoCapture && (IsUsbDevice(deviceInfo) || IsLikelyCamera(deviceInfo)))
                            {
                                cameras.Add(deviceInfo);
                                Console.WriteLine($"   ✅ Добавлено как камера для захвата видео");
                            }
                            else
                            {
                                string reason = "";
                                if (!deviceInfo.SupportsVideoCapture) reason += "нет захвата видео; ";
                                if (!IsUsbDevice(deviceInfo) && !IsLikelyCamera(deviceInfo)) reason += "не определено как камера; ";
                                Console.WriteLine($"   ❌ Пропущено: {reason.TrimEnd(';', ' ')}");
                            }
                            Console.WriteLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Ошибка при обработке устройства {devicePath}: {ex.Message}");
                        Console.WriteLine();
                    }
                }

                Console.WriteLine(new string('=', 80));
                Console.WriteLine($"🎯 Итого найдено пригодных камер: {cameras.Count}");
                
                if (cameras.Count > 0)
                {
                    Console.WriteLine("📷 Пригодные для использования камеры:");
                    for (int i = 0; i < cameras.Count; i++)
                    {
                        var cam = cameras[i];
                        Console.WriteLine($"   {i + 1}. {cam.DevicePath} - {cam.Name} ({cam.DeviceType})");
                    }
                }
                else
                {
                    Console.WriteLine("⚠️  Не найдено камер, пригодных для захвата видео!");
                    Console.WriteLine("💡 Советы:");
                    Console.WriteLine("   - Проверьте подключение USB камеры");
                    Console.WriteLine("   - Установите v4l-utils: sudo apt install v4l-utils");
                    Console.WriteLine("   - Проверьте права доступа: ls -la /dev/video*");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при поиске веб-камер: {ex.Message}");
            }

            return cameras;
        }

        /// <summary>
        /// Быстрая проверка камер без подробного вывода (для использования в коде)
        /// </summary>
        /// <returns>Список камер, поддерживающих захват видео</returns>
        public List<CameraInfo> FindWorkingCameras()
        {
            var cameras = new List<CameraInfo>();

            try
            {
                var videoDevices = Directory.GetFiles("/dev", "video*")
                    .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^video\d+$"))
                    .OrderBy(f => f)
                    .ToList();

                foreach (var devicePath in videoDevices)
                {
                    try
                    {
                        var deviceInfo = GetDeviceInfo(devicePath);
                        if (deviceInfo != null && deviceInfo.SupportsVideoCapture && 
                            (IsUsbDevice(deviceInfo) || IsLikelyCamera(deviceInfo)))
                        {
                            cameras.Add(deviceInfo);
                        }
                    }
                    catch
                    {
                        // Тихо пропускаем ошибки в быстром режиме
                    }
                }

                // Fallback: если ничего не найдено через v4l2-ctl, попробуем простой способ
                if (cameras.Count == 0)
                {
                    cameras.AddRange(FindBasicCameras());
                }
            }
            catch
            {
                // Тихо пропускаем ошибки в быстром режиме
            }

            return cameras;
        }

        /// <summary>
        /// Простой поиск камер без использования v4l2-ctl (fallback метод)
        /// </summary>
        /// <returns>Список базовых камер</returns>
        public List<CameraInfo> FindBasicCameras()
        {
            var cameras = new List<CameraInfo>();

            try
            {
                // Простой поиск всех /dev/video* устройств
                var videoDevices = Directory.GetFiles("/dev", "video*")
                    .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^video\d+$"))
                    .OrderBy(f => f)
                    .ToList();

                Console.WriteLine($"🔧 Fallback: найдено {videoDevices.Count} видеоустройств без анализа возможностей");

                foreach (var devicePath in videoDevices)
                {
                    try
                    {
                        // Проверяем, можем ли мы открыть устройство для чтения
                        if (File.Exists(devicePath))
                        {
                            var deviceNumber = Path.GetFileName(devicePath);
                            var basicCamera = new CameraInfo
                            {
                                Name = $"Видеоустройство {deviceNumber}",
                                DevicePath = devicePath,
                                Description = "Базовое видеоустройство (без анализа v4l2)",
                                DeviceType = "Basic Camera",
                                IsConnected = true,
                                SupportsVideoCapture = true, // Предполагаем поддержку
                                SupportsStreaming = true,
                                SupportedFormats = new List<string> { "Unknown" },
                                Capabilities = "Неизвестно (v4l2-ctl недоступен)"
                            };

                            cameras.Add(basicCamera);
                            Console.WriteLine($"   📹 {devicePath} - добавлено как базовая камера");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"   ❌ {devicePath} - ошибка: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка простого поиска камер: {ex.Message}");
            }

            return cameras;
        }

        /// <summary>
        /// Получение информации об устройстве в Linux
        /// </summary>
        private CameraInfo? GetDeviceInfo(string devicePath)
        {
            try
            {
                // Получаем информацию через v4l2-ctl если доступно
                var v4lInfo = ExecuteCommand($"v4l2-ctl --device={devicePath} --info");
                var v4lCapabilities = ExecuteCommand($"v4l2-ctl --device={devicePath} --list-formats-ext");
                
                // Получаем информацию через udevadm
                var udevInfo = ExecuteCommand($"udevadm info --name={devicePath}");

                var name = ExtractDeviceName(v4lInfo, udevInfo, devicePath);
                var description = ExtractDeviceDescription(v4lInfo, udevInfo);
                var usbInfo = ExtractUsbInfo(udevInfo);
                var capabilities = ExtractCapabilities(v4lInfo);
                var formats = ExtractSupportedFormats(v4lCapabilities);
                var deviceType = DetermineDeviceType(v4lInfo, v4lCapabilities, name);

                return new CameraInfo
                {
                    Name = name,
                    DevicePath = devicePath,
                    Description = description,
                    DeviceID = usbInfo.DeviceId,
                    PnpDeviceID = usbInfo.PnpId,
                    IsConnected = File.Exists(devicePath),
                    Capabilities = capabilities,
                    SupportsVideoCapture = capabilities.Contains("Video Capture") || capabilities.Contains("video-capture"),
                    SupportsStreaming = capabilities.Contains("Streaming") || capabilities.Contains("streaming"),
                    SupportedFormats = formats,
                    DeviceType = deviceType
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения информации об устройстве {devicePath}: {ex.Message}");
            }

            return null;
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
        /// Извлечение названия устройства из вывода команд
        /// </summary>
        private string ExtractDeviceName(string v4lInfo, string udevInfo, string devicePath)
        {
            // Попытка извлечь название из v4l2-ctl
            var v4lMatch = Regex.Match(v4lInfo, @"Card type\s*:\s*(.+)", RegexOptions.IgnoreCase);
            if (v4lMatch.Success)
            {
                return v4lMatch.Groups[1].Value.Trim();
            }

            // Попытка извлечь из udev ID_MODEL
            var udevMatch = Regex.Match(udevInfo, @"ID_MODEL=(.+)", RegexOptions.IgnoreCase);
            if (udevMatch.Success)
            {
                return udevMatch.Groups[1].Value.Trim().Replace("_", " ");
            }

            // Попытка извлечь из udev ID_V4L_PRODUCT
            var v4lProductMatch = Regex.Match(udevInfo, @"ID_V4L_PRODUCT=(.+)", RegexOptions.IgnoreCase);
            if (v4lProductMatch.Success)
            {
                return v4lProductMatch.Groups[1].Value.Trim();
            }

            // Попытка извлечь из udev ID_VENDOR_ENC и ID_MODEL_ENC
            var vendorMatch = Regex.Match(udevInfo, @"ID_VENDOR_ENC=(.+)", RegexOptions.IgnoreCase);
            var modelMatch = Regex.Match(udevInfo, @"ID_MODEL_ENC=(.+)", RegexOptions.IgnoreCase);
            
            if (vendorMatch.Success && modelMatch.Success)
            {
                var vendor = Uri.UnescapeDataString(vendorMatch.Groups[1].Value.Replace(@"\x20", " "));
                var model = Uri.UnescapeDataString(modelMatch.Groups[1].Value.Replace(@"\x20", " "));
                return $"{vendor} {model}".Trim();
            }

            return $"Камера {Path.GetFileName(devicePath)}";
        }

        /// <summary>
        /// Извлечение описания устройства
        /// </summary>
        private string ExtractDeviceDescription(string v4lInfo, string udevInfo)
        {
            var driverMatch = Regex.Match(v4lInfo, @"Driver name\s*:\s*(.+)", RegexOptions.IgnoreCase);
            if (driverMatch.Success)
            {
                return $"Video4Linux устройство ({driverMatch.Groups[1].Value.Trim()})";
            }

            // Попытка найти драйвер в udev
            var udevDriverMatch = Regex.Match(udevInfo, @"ID_V4L_CAPABILITIES=(.+)", RegexOptions.IgnoreCase);
            if (udevDriverMatch.Success)
            {
                return $"Video4Linux устройство (возможности: {udevDriverMatch.Groups[1].Value.Trim()})";
            }

            return "USB видеоустройство";
        }

        /// <summary>
        /// Извлечение USB информации
        /// </summary>
        private (string DeviceId, string PnpId) ExtractUsbInfo(string udevInfo)
        {
            var vendorMatch = Regex.Match(udevInfo, @"ID_VENDOR_ID=([0-9a-fA-F]+)");
            var productMatch = Regex.Match(udevInfo, @"ID_MODEL_ID=([0-9a-fA-F]+)");

            if (vendorMatch.Success && productMatch.Success)
            {
                var vendorId = vendorMatch.Groups[1].Value;
                var productId = productMatch.Groups[1].Value;
                var deviceId = $"USB\\VID_{vendorId}&PID_{productId}";
                return (deviceId, deviceId);
            }

            return (string.Empty, string.Empty);
        }

        /// <summary>
        /// Проверка, является ли устройство USB
        /// </summary>
        private bool IsUsbDevice(CameraInfo deviceInfo)
        {
            return !string.IsNullOrEmpty(deviceInfo.DeviceID) && 
                   deviceInfo.DeviceID.Contains("USB", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверка, похоже ли устройство на камеру
        /// </summary>
        private bool IsLikelyCamera(CameraInfo deviceInfo)
        {
            var nameAndDesc = $"{deviceInfo.Name} {deviceInfo.Description}".ToLower();
            
            var cameraKeywords = new[] { "camera", "webcam", "usb", "video", "cam", "capture" };
            
            return cameraKeywords.Any(keyword => nameAndDesc.Contains(keyword));
        }

        /// <summary>
        /// Извлечение возможностей устройства из v4l2-ctl --info
        /// </summary>
        private string ExtractCapabilities(string v4lInfo)
        {
            var capabilitiesMatch = Regex.Match(v4lInfo, @"Device Caps\s*:\s*(.+)", RegexOptions.IgnoreCase);
            if (capabilitiesMatch.Success)
            {
                return capabilitiesMatch.Groups[1].Value.Trim();
            }

            // Альтернативный поиск
            var altMatch = Regex.Match(v4lInfo, @"Capabilities\s*:\s*(.+)", RegexOptions.IgnoreCase);
            if (altMatch.Success)
            {
                return altMatch.Groups[1].Value.Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Извлечение поддерживаемых форматов из v4l2-ctl --list-formats-ext
        /// </summary>
        private List<string> ExtractSupportedFormats(string v4lFormats)
        {
            var formats = new List<string>();
            
            // Поиск форматов типа YUYV, MJPG, H264, etc.
            var formatMatches = Regex.Matches(v4lFormats, @"'([A-Z0-9]{4})'", RegexOptions.IgnoreCase);
            
            foreach (Match match in formatMatches)
            {
                var format = match.Groups[1].Value;
                if (!formats.Contains(format))
                {
                    formats.Add(format);
                }
            }

            // Если форматы не найдены, попробуем другой способ
            if (formats.Count == 0)
            {
                var pixelFormatMatches = Regex.Matches(v4lFormats, @"Pixel Format\s*:\s*'([^']+)'", RegexOptions.IgnoreCase);
                foreach (Match match in pixelFormatMatches)
                {
                    var format = match.Groups[1].Value;
                    if (!formats.Contains(format))
                    {
                        formats.Add(format);
                    }
                }
            }

            return formats;
        }

        /// <summary>
        /// Определение типа устройства (Camera, Metadata, etc.)
        /// </summary>
        private string DetermineDeviceType(string v4lInfo, string v4lFormats, string deviceName)
        {
            var capabilities = ExtractCapabilities(v4lInfo).ToLower();
            var formats = ExtractSupportedFormats(v4lFormats);

            // Проверяем на metadata устройство
            if (capabilities.Contains("metadata") || deviceName.ToLower().Contains("metadata"))
            {
                return "Metadata";
            }

            // Проверяем на обычную камеру
            if (capabilities.Contains("video capture") || capabilities.Contains("video-capture"))
            {
                if (formats.Count > 0)
                {
                    return "Camera";
                }
                else
                {
                    return "Video Device";
                }
            }

            // Проверяем на устройство вывода
            if (capabilities.Contains("video output"))
            {
                return "Video Output";
            }

            // Неизвестный тип
            return "Unknown";
        }

        /// <summary>
        /// Проверка наличия утилиты v4l2-ctl
        /// </summary>
        private bool CheckV4l2Utils()
        {
            try
            {
                var result = ExecuteCommand("which v4l2-ctl");
                return !string.IsNullOrWhiteSpace(result);
            }
            catch
            {
                return false;
            }
        }
    }
}
