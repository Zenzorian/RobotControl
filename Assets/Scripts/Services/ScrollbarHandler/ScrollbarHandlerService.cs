using UnityEngine;
using UnityEngine.UI;
using Scripts.UI.Markers;

namespace Scripts.Services
{
    public class SliderHandlerService
    {
        public float LeftSliderValue
        {
            get => _leftSliderValue;
            set
            {
            _leftSliderValue = value;
            _uiSliders.leftSlider.value = value;
            }
        }

        public float RightSliderValue
        {
            get => _rightSliderValue;
            set
            {
                _rightSliderValue = value;
                _uiSliders.rightSlider.value = value;
            }
        }

        public float CameraSliderValue
        {
            get => _cameraSliderValue;
            set
            {
                _cameraSliderValue = value;
                _uiSliders.cameraSlider.value = value;
            }
        }

        private UISliders _uiSliders;

        private float _leftSliderValue;
        private float _rightSliderValue;
        private float _cameraSliderValue;

        private IInputManagerService _inputManagerService;

        public SliderHandlerService(UISliders uiSliders,IInputManagerService inputManagerService)
        {
            _uiSliders = uiSliders;
            _inputManagerService = inputManagerService;
        }

        public void Initialize()
        {        
            _inputManagerService.OnValueChanged += OnValueChanged;

            _uiSliders.leftSlider.onValueChanged.AddListener(value => LeftSliderValue = value);
            _uiSliders.rightSlider.onValueChanged.AddListener(value => RightSliderValue = value);
            _uiSliders.cameraSlider.onValueChanged.AddListener(value => CameraSliderValue = value);
        }
        public void Dispose()
        {
            _inputManagerService.OnValueChanged -= OnValueChanged;
        }   
        private void OnValueChanged()
        {
            LeftSliderValue = _inputManagerService.LeftStickValue.y;
            RightSliderValue = _inputManagerService.RightStickValue.y;
            SetCameraSliderValue(); 
        }

        private void SetCameraSliderValue()
        {
            if (_inputManagerService.DpadValue == 0) return;
            CameraSliderValue = _inputManagerService.DpadValue;
        }
}
}