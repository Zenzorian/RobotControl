using UnityEngine;
using UnityEngine.UI;

public class SpeedLimiter : MonoBehaviour
{
    public float Value
    {
        get => _value;
        set
        {
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            _value = value;
            _slider.value = value;
            _text.text = $"Швидкість: {value}%";
        }
    }

    [SerializeField] private Slider _slider;
    [SerializeField] private Text _text;
    private float _value;
   
   private void Start()
   {
    _slider.onValueChanged.AddListener(OnSliderValueChanged);
   }

   private void OnSliderValueChanged(float value) => Value = value;
}
