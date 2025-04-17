using Scripts.Services;
using Scripts.UI.Markers;
using WebSocketSharp;
using UnityEngine;
using Scripts.UI;

namespace Scripts.Infrastructure
{
    public class Bootstrapper : MonoBehaviour
    {     
        [SerializeField] private ServerAddressField _serverAddressField;
        private IInputManagerService _inputManagerService;  
        private IStatus _status;
        private IWebSocketClient _webSocketClient;
        private ICommandSenderService _commandSenderService;

        //private const string SERVER_THUMBPRINT = "65863CD6EF075DF79E002205FCAB4FC2B4DD2223E6B3CAB7B8730EE307E79B57";
        //private float _logStatusTimer = 0f;
        //private bool _previousConnectionStatus = false;

        private void Awake()
        {
            var uiSliders = FindFirstObjectByType<UISliders>();

            InitializeInputManagerService();

            InitializeStatus();

            InitializeSliderHandlerService(uiSliders, _inputManagerService);

            _serverAddressField.OnConnectButtonClicked.AddListener(InitializeWebSocketService);
            
            Debug.Log("Bootstrapper: Инициализация завершена");
        }

        private void InitializeInputManagerService()
        {           
            _inputManagerService = new GamepadInputManagerService();
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
        }
        private void Update()
        {
            _inputManagerService?.Update();
            _webSocketClient?.Update();
            _commandSenderService?.Update(Time.deltaTime);
        }
    }
}
