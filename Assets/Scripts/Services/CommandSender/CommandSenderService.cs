using UnityEngine;
using System;

namespace Scripts.Services
{
    public class CommandSenderService : ICommandSenderService
    {
        private readonly IInputManagerService _inputManager;
        private readonly IWebSocketClient _webSocketClient;
        private readonly IStatus _status;

        private float _commandTimer = 0f;
        private float _commandInterval = 0.05f; // 20 команд в секунду по умолчанию
        
        private Command _lastCommand = new Command();       
          
               
        public int CommandsPerSecond
        {
            get { return Mathf.RoundToInt(1f / _commandInterval); }
            set { _commandInterval = Mathf.Clamp(1f / value, 0.016f, 1f); } // от 1 до 60 команд в секунду
        }        

        public CommandSenderService(IInputManagerService inputManager, IWebSocketClient webSocketClient, IStatus status)
        {
            _inputManager = inputManager;
            _webSocketClient = webSocketClient;
            _status = status;
            
            _inputManager.OnValueChanged += OnValueChanged;
        }

        public void Update(float deltaTime)
        {           
            if (!_webSocketClient.IsConnected)return;
                
            _commandTimer += deltaTime;            
           
            if (_commandTimer >= _commandInterval)
            {
                _commandTimer = 0f;
                //SendCommand();
            }
        }

        private void SendCommand()
        {
            try
            {               
                string command = FormatCommand(_lastCommand);                
              
                _webSocketClient.GetWebSocket.Send(command);               
            }
            catch (Exception ex)
            {
                _status.Error($"Ошибка отправки команды: {ex.Message}");
            }
        }

        private string FormatCommand(Command command)
        {  
            Command scaledCommand = command;           
            
            string jsonData = JsonUtility.ToJson(scaledCommand);         

            return $"COMMAND!{jsonData}";           
        }

        private void OnValueChanged()
        {
            _lastCommand.cameraAngle = _inputManager.CameraAngle;

            _lastCommand.leftStickValue = _inputManager.LeftStickValue;
            _lastCommand.rightStickValue = _inputManager.RightStickValue;   
                    
            SendCommand();
        }         
       
        public void Dispose()
        {
            if (_inputManager == null) return;            
            _inputManager.OnValueChanged -= OnValueChanged;
        }
    }
    
    [Serializable]
    public struct Command
    {
        public Vector2 leftStickValue;
        public Vector2 rightStickValue; 
        public float cameraAngle;      
    }
} 