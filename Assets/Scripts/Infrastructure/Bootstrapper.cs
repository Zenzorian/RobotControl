using UnityEngine;
using Scripts.Services;
using Scripts.UI.Markers;

namespace Scripts.Infrastructure
{
    public class Bootstrapper : MonoBehaviour
    {     
        [SerializeField] private ServerAddressField _serverAddressField;
        private IInputManagerService _inputManagerService;       
        private const string SERVER_THUMBPRINT = "65863CD6EF075DF79E002205FCAB4FC2B4DD2223E6B3CAB7B8730EE307E79B57";
        private float _logStatusTimer = 0f;
        private bool _previousConnectionStatus = false;

        private void Awake()
        {
            var uiSliders = FindFirstObjectByType<UISliders>();

            _inputManagerService = InitializeInputManagerService();

            InitializeSliderHandlerService(uiSliders, _inputManagerService);
            _serverAddressField.OnConnectButtonClicked.AddListener(InitializeServerAddressFieldService);
            
            Debug.Log("Bootstrapper: Инициализация завершена");
        }

        private IInputManagerService InitializeInputManagerService()
        {
            //return new GamepadInputManagerService();
            return new KeyboardInputManagerService();
        }
        private void InitializeSliderHandlerService(UISliders uiSliders, IInputManagerService inputManagerService)
        {
           var sliderHandlerService = new SliderHandlerService(uiSliders, inputManagerService);
            sliderHandlerService.Initialize();
        }
        private void InitializeServerAddressFieldService(string serverAddress, int serverPort)
        {   
            
        }      
    }
}
