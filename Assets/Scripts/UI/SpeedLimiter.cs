using UnityEngine;
using UnityEngine.UI;
using System;
public class SpeedLimiter : MonoBehaviour
{
    public Action<float> OnSpeedChanged;    
    public float Value
    {
        get => _value;
        set
        {
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            _value = value;
            _slider.value = value;
            _text.text = $"Швидкість: {Mathf.Round(value*100)}%";
        }
    }

    [SerializeField] private Slider _slider;
    [SerializeField] private Text _text;
    private float _value = 1;
   
   private void Start()
   {
    _slider.onValueChanged.AddListener(OnSliderValueChanged);
    _slider.onValueChanged.AddListener((value) => OnSpeedChanged?.Invoke(value));
   }

   private void OnSliderValueChanged(float value) => Value = value;
}
