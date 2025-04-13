using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text.RegularExpressions;

namespace RobotControl
{
    [RequireComponent(typeof(TMP_InputField))]
    public class IPAddressInputField : MonoBehaviour
    {
        private TMP_InputField _inputField;
        private string _lastValidIP = "127.0.0.1";
        private readonly Regex _ipRegex = new Regex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");

        private void Awake()
        {
            _inputField = GetComponent<TMP_InputField>();
            _inputField.onValueChanged.AddListener(OnValueChanged);
            _inputField.onEndEdit.AddListener(OnEndEdit);
            
            // Устанавливаем начальное значение
            _inputField.text = _lastValidIP;
        }

        private void OnValueChanged(string value)
        {
            // Разрешаем только цифры и точки
            if (!Regex.IsMatch(value, @"^[0-9.]*$"))
            {
                _inputField.text = _lastValidIP;
                return;
            }

            // Проверяем формат IP
            if (_ipRegex.IsMatch(value))
            {
                _lastValidIP = value;
            }
        }

        private void OnEndEdit(string value)
        {
            // При потере фокуса проверяем валидность IP
            if (!_ipRegex.IsMatch(value))
            {
                _inputField.text = _lastValidIP;
            }
        }

        public string GetIPAddress()
        {
            return _lastValidIP;
        }
    }
} 