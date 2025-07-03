namespace RobotClient.Config
{
    /// <summary>
    /// Конфигурация сервера робота
    /// </summary>
    public static class ServerConfig
    {
        /// <summary>
        /// IP адрес сервера
        /// </summary>
        public const string SERVER_IP = "193.169.240.11";
        
        /// <summary>
        /// Порт сервера
        /// </summary>
        public const int SERVER_PORT = 8080;
        
        /// <summary>
        /// Полный URL WebSocket сервера
        /// </summary>
        public static string WebSocketUrl => $"ws://{SERVER_IP}:{SERVER_PORT}";
        
        /// <summary>
        /// HTTP URL сервера (для REST API, если понадобится)
        /// </summary>
        public static string HttpUrl => $"http://{SERVER_IP}:{SERVER_PORT}";
        
        /// <summary>
        /// HTTPS URL сервера (для защищенных соединений)
        /// </summary>
        public static string HttpsUrl => $"https://{SERVER_IP}:{SERVER_PORT}";
        
        /// <summary>
        /// WSS URL сервера (для защищенных WebSocket соединений)
        /// </summary>
        public static string SecureWebSocketUrl => $"wss://{SERVER_IP}:{SERVER_PORT}";
        
        /// <summary>
        /// Таймаут подключения в секундах
        /// </summary>
        public const int CONNECTION_TIMEOUT_SECONDS = 30;
        
        /// <summary>
        /// Интервал отправки телеметрии в секундах
        /// </summary>
        public const int TELEMETRY_INTERVAL_SECONDS = 30;
        
        /// <summary>
        /// Интервал проверки соединения в секундах
        /// </summary>
        public const int HEALTH_CHECK_INTERVAL_SECONDS = 60;
        
        /// <summary>
        /// Максимальное количество попыток переподключения
        /// </summary>
        public const int MAX_RECONNECT_ATTEMPTS = 5;
        
        /// <summary>
        /// Задержка между попытками переподключения в секундах
        /// </summary>
        public const int RECONNECT_DELAY_SECONDS = 5;
    }
} 