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
        _inputManagerService.OnLeftStickValueChanged += OnLeftStickValueChanged;
        _inputManagerService.OnRightStickValueChanged += OnRightStickValueChanged;
        _inputManagerService.OnDpadValueChanged += OnDpadValueChanged;

        _uiSliders.leftSlider.onValueChanged.AddListener(value => LeftSliderValue = value);
        _uiSliders.rightSlider.onValueChanged.AddListener(value => RightSliderValue = value);
        _uiSliders.cameraSlider.onValueChanged.AddListener(value => CameraSliderValue = value);
    }
    public void Dispose()
    {
        _inputManagerService.OnLeftStickValueChanged -= OnLeftStickValueChanged;
        _inputManagerService.OnRightStickValueChanged -= OnRightStickValueChanged;
        _inputManagerService.OnDpadValueChanged -= OnDpadValueChanged;
    }   
    private void OnLeftStickValueChanged(Vector2 value) => LeftSliderValue = value.y;

    private void OnRightStickValueChanged(Vector2 value) => RightSliderValue = value.y;

    private void OnDpadValueChanged(float value)
    {
        CameraSliderValue = value;
    }
}
}