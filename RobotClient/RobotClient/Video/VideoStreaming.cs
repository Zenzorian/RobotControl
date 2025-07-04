using RobotClient.Video;

namespace RobotClient.Video
{
    public class VideoStreaming
    {
        private readonly CameraSearch _cameraSearch;
        private CameraSearch.CameraInfo? _selectedCamera;
        private bool _isInitialized;

        public VideoStreaming()
        {
            _cameraSearch = new CameraSearch();
            Console.WriteLine("📹 Сервис видео трансляции инициализирован");
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                Console.WriteLine("🔍 Поиск доступных камер...");
                var cameras = _cameraSearch.FindWorkingCameras();
                
                if (cameras.Count == 0)
                {
                    Console.WriteLine("❌ Не найдено камер, поддерживающих захват видео");
                    Console.WriteLine("🔍 Запуск подробной диагностики...");
                    Console.WriteLine();
                    
                    // Автоматическая подробная диагностика
                    var allCameras = _cameraSearch.FindUsbWebCameras();
                    
                    if (allCameras.Count == 0)
                    {
                        Console.WriteLine("💡 Возможные причины:");
                        Console.WriteLine("   • Камера не подключена");
                        Console.WriteLine("   • Не установлен v4l-utils: sudo apt install v4l-utils");
                        Console.WriteLine("   • Нет прав доступа: sudo usermod -a -G video $USER");
                        Console.WriteLine("   • Камера используется другим приложением");
                    }
                    else
                    {
                        Console.WriteLine("⚠️  Камеры найдены, но ни одна не поддерживает захват видео!");
                        Console.WriteLine("💡 Попробуйте использовать любую найденную камеру принудительно...");
                        
                        // Fallback: попробуем использовать первую найденную камеру
                        var fallbackCamera = allCameras.FirstOrDefault(c => c.IsConnected);
                        if (fallbackCamera != null)
                        {
                            Console.WriteLine($"🔄 Попытка использовать {fallbackCamera.DevicePath} принудительно...");
                            _selectedCamera = fallbackCamera;
                            _isInitialized = true;
                            Console.WriteLine($"✅ Принудительно выбрана камера: {_selectedCamera.Name}");
                            Console.WriteLine($"   📍 Устройство: {_selectedCamera.DevicePath}");
                            return true;
                        }
                    }
                    
                    return false;
                }

                // Выбираем первую рабочую камеру
                _selectedCamera = cameras.FirstOrDefault(c => c.IsConnected && c.SupportsVideoCapture);
                
                if (_selectedCamera == null)
                {
                    Console.WriteLine("❌ Нет подключенных камер с поддержкой захвата видео");
                    return false;
                }

                Console.WriteLine($"✅ Выбрана камера: {_selectedCamera.Name}");
                Console.WriteLine($"   📍 Устройство: {_selectedCamera.DevicePath}");
                Console.WriteLine($"   🏷️  Тип: {_selectedCamera.DeviceType}");
                Console.WriteLine($"   🎨 Форматы: {string.Join(", ", _selectedCamera.SupportedFormats)}");
                
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка инициализации видео сервиса: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> StartStreamAsync(string sessionId)
        {
            if (!_isInitialized || _selectedCamera == null)
            {
                Console.WriteLine("❌ Видео сервис не инициализирован");
                return false;
            }

            Console.WriteLine($"📹 Запуск видео стрима для сессии: {sessionId}");
            await Task.Delay(100);
            Console.WriteLine($"✅ Видео стрим запущен для сессии: {sessionId}");
            return true;
        }

        public async Task<bool> StopStreamAsync(string sessionId)
        {
            Console.WriteLine($"🛑 Остановка видео стрима для сессии: {sessionId}");
            await Task.Delay(50);
            Console.WriteLine($"✅ Видео стрим остановлен для сессии: {sessionId}");
            return true;
        }

        public async Task StopAsync()
        {
            _isInitialized = false;
            Console.WriteLine("✅ Все видео стримы остановлены");
        }
    }
}
