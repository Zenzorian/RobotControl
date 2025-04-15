using System;
using System.Threading.Tasks;
using UnityEngine;

namespace RobotControl
{
    public class RobotControlClient : WebSocketClient
    {
        public RobotControlClient(string ipAddress, int port, string serverThumbprint) 
            : base(ipAddress, port, "ws/control", serverThumbprint)
        {
        }

        public async Task<bool> SendMovementCommandAsync(int leftWheels, int rightWheels, int speed)
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Немає з'єднання з роботом");
                return false;
            }

            try
            {
                var command = new MovementCommand { 
                    type = "movement",
                    leftWheels = leftWheels, 
                    rightWheels = rightWheels, 
                    speed = speed 
                };
                string json = JsonUtility.ToJson(command);
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Помилка відправки команди руху: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendCameraCommandAsync(int angle)
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Немає з'єднання з роботом");
                return false;
            }

            try
            {
                var command = new CameraCommand { 
                    type = "camera",
                    angle = angle 
                };
                string json = JsonUtility.ToJson(command);
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Помилка відправки команди камери: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendStopCommand()
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Немає з'єднання з роботом");
                return false;
            }

            try
            {
                var command = new MovementCommand { 
                    type = "movement",
                    leftWheels = 0, 
                    rightWheels = 0, 
                    speed = 0 
                };
                string json = JsonUtility.ToJson(command);
                bool result = await SendMessageAsync(json);
                
                if (result)
                {
                    OnConnectionStatusChanged("Відправлено команду аварійної зупинки");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Помилка відправки команди зупинки: {ex.Message}");
                return false;
            }
        }

        protected override void OnMessageReceived(string message)
        {
            // Обробка відповідей від робота
            try
            {
                var response = JsonUtility.FromJson<RobotResponse>(message);
                if (response != null)
                {
                    // Обробка різних типів відповідей
                    switch (response.type)
                    {
                        case "status":
                            // Обробка статусу
                            break;
                        case "error":
                            // Обробка помилки
                            OnErrorOccurred($"Помилка від робота: {response.message}");
                            break;
                    }
                }
            }
            catch
            {
                // Ігноруємо помилки парсингу
            }
        }
    }

    [Serializable]
    public class MovementCommand
    {
        public string type;
        public int leftWheels;
        public int rightWheels;
        public int speed;
    }

    [Serializable]
    public class CameraCommand
    {
        public string type;
        public int angle;
    }

    [Serializable]
    public class RobotResponse
    {
        public string type;
        public string message;
    }
} 