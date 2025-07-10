using RobotClient.Core;
using RobotClient.Config;

namespace RobotClient
{
    /// <summary>
    /// Главная точка входа для робота с двухпоточной архитектурой:
    /// - Поток управления роботом (команды, телеметрия, Pixhawk)
    /// - Поток трансляции видео (камера, WebRTC, видео сигналинг)
    /// </summary>
    class RobotMain
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Robot Client с двухпоточной архитектурой ===");
            Console.WriteLine("🔄 После подключения к WebSocket запустятся два потока:");
            Console.WriteLine("   🎮 Поток управления роботом");
            Console.WriteLine("   📹 Поток трансляции видео");
            Console.WriteLine();

            // Настройка обработки Ctrl+C
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                Console.WriteLine("\n🛑 Получен сигнал остановки двухпоточного робота...");
            };

            // Получение URL сервера из аргументов или переменной окружения
            string serverUrl = GetServerUrl(args);
            Console.WriteLine($"🔗 Сервер: {serverUrl}");
            Console.WriteLine();

            // Создание и запуск двухпоточного сервиса робота
            using var dualThreadRobotService = new DualThreadRobotService(args,serverUrl);

            try
            {
                // Запуск двухпоточного робота в фоновом режиме
                await dualThreadRobotService.RunAsync(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка в двухпоточном роботе: {ex.Message}");
                Environment.Exit(1);
            }

            Console.WriteLine("✅ Двухпоточный робот завершил работу");
        }

        /// <summary>
        /// Получение URL сервера из аргументов командной строки или переменных окружения
        /// </summary>
        private static string GetServerUrl(string[] args)
        {
            // Проверяем аргументы командной строки
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                return args[0];
            }

            // Проверяем переменную окружения
            string? envUrl = Environment.GetEnvironmentVariable("ROBOT_SERVER_URL");
            if (!string.IsNullOrEmpty(envUrl))
            {
                return envUrl;
            }

            // Значение по умолчанию из конфигурации
            return ServerConfig.WebSocketUrl;
        }
    }
} 