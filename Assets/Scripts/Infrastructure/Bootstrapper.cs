using UnityEngine;
using Scripts.Services;
using Scripts.UI.Markers;
using RobotControl;

namespace Scripts.Infrastructure
{
    public class Bootstrapper : MonoBehaviour
    {     
        [SerializeField] private ServerAddressField _serverAddressField;
        private IInputManagerService _inputManagerService;
        private IRobotClient _robotClient;

        private void Awake()
        {
            var uiSliders = FindFirstObjectByType<UISliders>();

            _inputManagerService = InitializeInputManagerService();

            InitializeSliderHandlerService(uiSliders, _inputManagerService);
            InitializeServerAddressFieldService(_serverAddressField);
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
        private void InitializeServerAddressFieldService(ServerAddressField serverAddressField)
        {            
            _robotClient = new RobotClient(_serverAddressField.OnConnectButtonClicked);            
        }
        private void Update()
        {
            if (_inputManagerService != null) _inputManagerService.Update();
            
        }
    }
}
