using UnityEngine;
using Scripts.Services;
using Scripts.UI.Markers;

namespace Scripts.Infrastructure
{
    public class Bootstrapper : MonoBehaviour
    {     
        private IInputManagerService _inputManagerService;

        private void Start()
        {
            var uiSliders = FindFirstObjectByType<UISliders>();

            _inputManagerService = InitializeInputManagerService();

            InitializeSliderHandlerService(uiSliders, _inputManagerService);
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
        private void Update()
        {
            if (_inputManagerService != null) _inputManagerService.Update();
            
        }
    }
}
