using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using UnityEngine.Events;
public class ServerAddressField : MonoBehaviour
{
    public UnityEvent<string, int> OnConnectButtonClicked;

    [SerializeField] private TMP_InputField _ipInputField;
    [SerializeField] private TMP_InputField _portInputField;
    [SerializeField] private Button _connectButton;  

    private string _lastValidIP = "193.169.240.11";
    private string _lastValidPort = "8080";
    private readonly Regex _ipRegex = new Regex(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");

    private void Start()
    {
        _ipInputField.onValueChanged.AddListener(OnValueChanged);
        _ipInputField.onEndEdit.AddListener(OnEndEdit);      

        _ipInputField.text = _lastValidIP;
        _portInputField.text = _lastValidPort;

        _connectButton.onClick.AddListener(() => OnConnectButtonClicked?.Invoke(_lastValidIP, int.Parse(_lastValidPort)));           
    }

    private void OnDisable()
    {
        _connectButton.onClick.RemoveListener(() => OnConnectButtonClicked?.Invoke(_lastValidIP, int.Parse(_lastValidPort)));
    }

    private void OnValueChanged(string value)
    {       
        if (!Regex.IsMatch(value, @"^[0-9.]*$"))
        {
            _ipInputField.text = _lastValidIP;
            return;
        }
       
        _lastValidIP = _ipRegex.IsMatch(value) ? value : _lastValidIP;
    }

    private void OnEndEdit(string value)=> 
        _ipInputField.text = !_ipRegex.IsMatch(value) ? _lastValidIP : value ;        
}
