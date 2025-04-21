using UnityEngine;
using Scripts.Services;
using System;
public class SettingsHandlerService
{    
    
    private readonly IInputManagerService _inputManagerService;   
    private readonly SettingsMarkers _settingsMarkers;
    private readonly Action<string, int> _initializeWebSocketService;


    private bool _isSettingsOpen = false;

    public SettingsHandlerService(IInputManagerService inputManagerService, SettingsMarkers settingsMarkers, Action<string, int> initializeWebSocketService)
    {
        _inputManagerService = inputManagerService;
        _settingsMarkers = settingsMarkers;
        _initializeWebSocketService = initializeWebSocketService;

        Initialize();
    }

    private void Initialize()
    {
        _settingsMarkers.settingsButton.onClick.AddListener(OpenSettings);
        _settingsMarkers.ServerAddressField.OnConnectButtonClicked
            .AddListener((serverAddress, port) => _initializeWebSocketService(serverAddress, port));
        _settingsMarkers.SpeedLimiter.OnSpeedChanged += UpdateSpeedCoefficient;
        _inputManagerService.OnValueChanged += SetSpeedCoefficient;
        _inputManagerService.OnValueChanged += OpenSettingsFromGamepad;
    }
    private void OpenSettingsFromGamepad()
    {
        if (_inputManagerService.OptionsPressed)
        {
            OpenSettings();
        }
    }
    private void OpenSettings()
    {
        _isSettingsOpen = !_isSettingsOpen;
        _settingsMarkers.settingsPanel.SetActive(_isSettingsOpen);
    }

    private void UpdateSpeedCoefficient(float speedCoefficient)
    {
        _inputManagerService.SpeedCoefficient = speedCoefficient;
    }

    private void SetSpeedCoefficient()
    {
            _settingsMarkers.SpeedLimiter.Value = _inputManagerService.SpeedCoefficient;
    }
   
}
