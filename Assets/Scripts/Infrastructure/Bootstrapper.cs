using Scripts.Services;
using Scripts.UI.Markers;
using WebSocketSharp;
using UnityEngine;
using Scripts.UI;
using System.Collections;

namespace Scripts.Infrastructure
{
    public class Bootstrapper : MonoBehaviour
    {     
        [SerializeField] private ServerAddressField _serverAddressField;
        private IInputManagerService _inputManagerService;  
        private IStatus _status;
        private IWebSocketClient _webSocketClient;
        private ICommandSenderService _commandSenderService;
        private IOptimizedRobotVideoService _videoService;

        //private const string SERVER_THUMBPRINT = "65863CD6EF075DF79E002205FCAB4FC2B4DD2223E6B3CAB7B8730EE307E79B57";
        //private float _logStatusTimer = 0f;
        //private bool _previousConnectionStatus = false;

        private void Awake()
        {
            var uiSliders = FindFirstObjectByType<UISliders>();

            InitializeInputManagerService();

            InitializeStatus();

            InitializeSliderHandlerService(uiSliders, _inputManagerService);          

            InitializeSettingsHandlerService();
            
            Debug.Log("Bootstrapper: Инициализация завершена");
        }

        private void InitializeInputManagerService()
        {           
            _inputManagerService = new GamepadInputManagerService();
            _inputManagerService.Reset();
        }
        private void InitializeSliderHandlerService(UISliders uiSliders, IInputManagerService inputManagerService)
        {
           var sliderHandlerService = new SliderHandlerService(uiSliders, inputManagerService);
            sliderHandlerService.Initialize();
        }
        private void InitializeStatus()
        {
            var statusMarker = FindFirstObjectByType<StatusMarker>();
            _status = new Status(statusMarker);
        }
        private void InitializeWebSocketService(string serverAddress, int serverPort)
        {
            if (_webSocketClient != null) _webSocketClient.Dispose();
            
            _webSocketClient = new WebSocketClient(serverAddress, serverPort, _status);

            _commandSenderService = new CommandSenderService(_inputManagerService, _webSocketClient, _status);
            
            // Инициализируем видео сервис
            InitializeVideoService();
        }

        private void InitializeVideoService()
        {
            Debug.Log("InitializeVideoService вызван");
            
            // Ищем OptimizedRobotVideoService компонент на сцене
            var videoServiceComponent = FindFirstObjectByType<OptimizedRobotVideoService>();
            if (videoServiceComponent != null)
            {
                Debug.Log($"OptimizedRobotVideoService найден: {videoServiceComponent.name}");
                _videoService = videoServiceComponent; // Используем как интерфейс
                
                try
                {
                    Debug.Log("Вызываем _videoService.Initialize...");
                    _videoService.Initialize(_webSocketClient, _status);
                    Debug.Log("_videoService.Initialize завершен успешно");
                    
                    // Подписываемся на события видео
                    _videoService.OnVideoConnectionChanged += OnVideoConnectionChanged;
                    _status.Info("Оптимизированный видео сервис инициализирован");
                    
                    // Видео будет запрошено автоматически в OptimizedRobotVideoService
                    // при получении REGISTERED!CONTROLLER сообщения
                }
                catch (System.Exception ex)
                {
                    _status.Error($"Ошибка инициализации видеосервиса: {ex.Message}");
                    Debug.LogError($"InitializeVideoService error: {ex}");
                }
            }
            else
            {
                _status.Error("OptimizedRobotVideoService не найден на сцене");
                Debug.LogError("OptimizedRobotVideoService не найден на сцене! Создайте GameObject с компонентом OptimizedRobotVideoService.");
            }
        }

        private void OnVideoConnectionChanged(bool isConnected)
        {
            if (isConnected)
            {
                _status.Info("Видеопоток подключен");
            }
            else
            {
                _status.Info("Видеопоток отключен");
            }
        }
        private void InitializeSettingsHandlerService()
        {
            var settingsMarkers = FindFirstObjectByType<SettingsMarkers>();
            var settingsHandlerService = new SettingsHandlerService(_inputManagerService, settingsMarkers, InitializeWebSocketService);
            
        }
        private void Update()
        {
            _inputManagerService?.Update();
            _webSocketClient?.Update();
            _commandSenderService?.Update(Time.deltaTime);
        }
    }
}
