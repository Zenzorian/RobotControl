using System;
using System.Threading.Tasks;
using UnityEngine;

namespace RobotControl
{
    public class RobotVideoClient : WebSocketClient
    {
        public event EventHandler<WebRTCSignalingMessage> WebRTCSignalingReceived;
        public event EventHandler<Texture2D> VideoFrameReceived;

        public RobotVideoClient(string ipAddress, int port) 
            : base(ipAddress, port, "ws/video")
        {
        }

        public async Task<bool> InitiateWebRTCConnection()
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Нет соединения с роботом");
                return false;
            }

            try
            {
                var command = new { type = "webrtc-init" };
                string json = JsonUtility.ToJson(command);
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка инициализации WebRTC: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendWebRTCSignaling(WebRTCSignalingMessage message)
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Нет соединения с роботом");
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(message);
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка отправки сигналинга WebRTC: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendVideoSettingsAsync(int quality, int fps)
        {
            if (!_isConnected)
            {
                OnErrorOccurred("Немає з'єднання з роботом");
                return false;
            }

            try
            {
                var settings = new VideoSettings { 
                    type = "settings",
                    quality = quality, 
                    fps = fps 
                };
                string json = JsonUtility.ToJson(settings);
                return await SendMessageAsync(json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Помилка відправки налаштувань відео: {ex.Message}");
                return false;
            }
        }

        protected override void OnMessageReceived(string message)
        {
            // Проверяем, является ли сообщение сигналингом WebRTC
            try
            {
                var signalingMessage = JsonUtility.FromJson<WebRTCSignalingMessage>(message);
                if (signalingMessage != null && !string.IsNullOrEmpty(signalingMessage.type) && 
                    (signalingMessage.type == "offer" || signalingMessage.type == "answer" || 
                     signalingMessage.type == "candidate" || signalingMessage.type == "ice-candidate"))
                {
                    WebRTCSignalingReceived?.Invoke(this, signalingMessage);
                    return;
                }
            }
            catch
            {
                // Игнорируем ошибки парсинга, если сообщение не является сигналингом WebRTC
            }

            // Обработка других типов сообщений
            try
            {
                var response = JsonUtility.FromJson<VideoResponse>(message);
                if (response != null)
                {
                    switch (response.type)
                    {
                        case "frame":
                            // Обробка кадру
                            break;
                        case "error":
                            // Обробка помилки
                            OnErrorOccurred($"Помилка від відео потоку: {response.message}");
                            break;
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки парсинга
            }
        }

        // Метод для обработки видеокадров от WebRTC
        public void ProcessVideoFrame(byte[] frameData, int width, int height)
        {
            try
            {
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.LoadRawTextureData(frameData);
                texture.Apply();
                
                VideoFrameReceived?.Invoke(this, texture);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Ошибка обработки видеокадра: {ex.Message}");
            }
        }
    }

    [Serializable]
    public class WebRTCSignalingMessage
    {
        public string type; // offer, answer, candidate, ice-candidate
        public string sdp;
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }

    [Serializable]
    public class VideoSettings
    {
        public string type;
        public int quality;
        public int fps;
    }

    [Serializable]
    public class VideoResponse
    {
        public string type;
        public string message;
        public string data; // Base64 encoded image data
    }
} 