using Scripts.Services;
using Scripts.UI.Markers;
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
        private IWebRTCVideoService _videoService;

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
            
            _webSocketClient = new WebSocketClient(_status);

            _commandSenderService = new CommandSenderService(_inputManagerService, _webSocketClient, _status);
            
            // Инициализируем видео сервис
            InitializeVideoService();
        }

        private void InitializeVideoService()
        {
            Debug.Log("InitializeVideoService вызван");
            
            // Ищем WebRTCVideoService компонент на сцене
            var videoServiceComponent = FindFirstObjectByType<WebRTCVideoService>();
            if (videoServiceComponent != null)
            {
                Debug.Log($"WebRTCVideoService найден: {videoServiceComponent.name}");
                _videoService = videoServiceComponent; // Используем как интерфейс
                
                try
                {
                    Debug.Log("Вызываем _videoService.Initialize...");
                    _videoService.Initialize(_webSocketClient);
                    Debug.Log("_videoService.Initialize завершен успешно");
                    
                    // Ищем RawImage компонент для отображения видео
                    var videoDisplay = FindFirstObjectByType<UnityEngine.UI.RawImage>();
                    if (videoDisplay != null)
                    {
                        Debug.Log($"🎬 RawImage найден: {videoDisplay.name}");
                        _videoService.SetVideoOutput(videoDisplay);
                        _status.Info($"Видео выход подключен: {videoDisplay.name}");
                    }
                    else
                    {
                        Debug.LogWarning("🎬 ⚠️ RawImage компонент не найден! Видео не будет отображаться");
                        _status.Warning("RawImage не найден - видео не будет отображаться");
                    }
                    
                    // Подписываемся на события видео
                    _videoService.OnConnectionStateChanged += OnVideoConnectionChanged;
                    _status.Info("WebRTC видео сервис инициализирован");
                    
                    // Автоматически запускаем WebRTC соединение после регистрации
                    StartCoroutine(StartWebRTCAfterRegistration());
                }
                catch (System.Exception ex)
                {
                    _status.Error($"Ошибка инициализации WebRTC видеосервиса: {ex.Message}");
                    Debug.LogError($"InitializeVideoService error: {ex}");
                }
            }
            else
            {
                _status.Error("WebRTCVideoService не найден на сцене");
                Debug.LogError("WebRTCVideoService не найден на сцене! Создайте GameObject с компонентом WebRTCVideoService.");
            }
        }

        private void OnVideoConnectionChanged(bool isConnected)
        {
            if (isConnected)
            {
                _status.Info("WebRTC видеопоток подключен");
            }
            else
            {
                _status.Info("WebRTC видеопоток отключен");
            }
        }
        
        private IEnumerator StartWebRTCAfterRegistration()
        {
            // Ждем короткое время для завершения инициализации
            yield return new WaitForSeconds(1f);
            
            // Проверяем что WebSocket подключен
            if (_webSocketClient != null && _webSocketClient.IsConnected)
            {
                // Запускаем WebRTC соединение
                if (_videoService != null)
                {
                    _videoService.StartConnection();
                    _status.Info("WebRTC соединение запускается...");
                }
            }
            else
            {
                _status.Warning("WebSocket не подключен, WebRTC соединение отложено");
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
