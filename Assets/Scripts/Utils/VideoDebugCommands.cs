using UnityEngine;
using Scripts.Services;

namespace Scripts.Utils
{
    public class VideoDebugCommands : MonoBehaviour
    {
        private IWebRTCVideoService _videoService;
        
        private void Start()
        {
            FindVideoService();
        }
        
        private void FindVideoService()
        {
            var videoServiceObj = GameObject.FindObjectOfType<WebRTCVideoService>();
            if (videoServiceObj != null)
            {
                _videoService = videoServiceObj;
                Debug.Log("VideoDebugCommands: WebRTC видео сервис найден");
            }
        }
        
        private void Update()
        {
            // Горячие клавиши для отладки WebRTC
            if (Input.GetKeyDown(KeyCode.F1))
            {
                CheckWebRTCStatus();
            }
            
            if (Input.GetKeyDown(KeyCode.F2))
            {
                LogDetailedWebRTCStats();
            }
            
            if (Input.GetKeyDown(KeyCode.F3))
            {
                ResetWebRTCStats();
            }
            
            if (Input.GetKeyDown(KeyCode.F4))
            {
                StartWebRTCConnection();
            }
            
            if (Input.GetKeyDown(KeyCode.F5))
            {
                StopWebRTCConnection();
            }
        }
        
        [ContextMenu("Check WebRTC Status")]
        public void CheckWebRTCStatus()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService == null)
            {
                Debug.LogError("❌ WebRTC видео сервис не найден!");
                return;
            }
            
            string status = $"🔍 БЫСТРАЯ ПРОВЕРКА WEBRTC:\n" +
                          $"• Подключено: {(_videoService.IsConnected ? "✅ ДА" : "❌ НЕТ")}\n" +
                          $"• Стриминг: {(_videoService.IsStreaming ? "✅ ДА" : "❌ НЕТ")}\n" +
                          $"• Состояние: {_videoService.ConnectionState}\n" +
                          $"• FPS: {_videoService.CurrentFPS:F1}\n" +
                          $"• Кадров получено: {_videoService.ReceivedFrames}\n" +
                          $"• Байт получено: {_videoService.BytesReceived:N0}";
            
            Debug.Log(status);
            
            if (!_videoService.IsConnected)
            {
                Debug.LogWarning("⚠️ WebRTC не подключен. Попробуйте F4 для запуска соединения.");
            }
            else if (!_videoService.IsStreaming)
            {
                Debug.LogWarning("⚠️ WebRTC подключен, но стриминг неактивен.");
            }
        }
        
        [ContextMenu("Log Detailed WebRTC Stats")]
        public void LogDetailedWebRTCStats()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                string report = _videoService.GetStatusReport();
                Debug.Log("📊 ПОДРОБНЫЙ ОТЧЕТ WEBRTC:\n" + report);
            }
            else
            {
                Debug.LogError("❌ WebRTC видео сервис не найден!");
            }
        }
        
        [ContextMenu("Reset WebRTC Stats")]
        public void ResetWebRTCStats()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                _videoService.ResetStatistics();
                Debug.Log("🔄 Статистика WebRTC сброшена");
            }
            else
            {
                Debug.LogError("❌ WebRTC видео сервис не найден!");
            }
        }
        
        [ContextMenu("Start WebRTC Connection")]
        public void StartWebRTCConnection()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                _videoService.StartConnection();
                Debug.Log("🚀 Запуск WebRTC соединения...");
            }
            else
            {
                Debug.LogError("❌ WebRTC видео сервис не найден!");
            }
        }
        
        [ContextMenu("Stop WebRTC Connection")]
        public void StopWebRTCConnection()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                _videoService.StopConnection();
                Debug.Log("⏹️ Остановка WebRTC соединения...");
            }
            else
            {
                Debug.LogError("❌ WebRTC видео сервис не найден!");
            }
        }
        
        private void OnGUI()
        {
            // Простой GUI для отладки WebRTC
            GUILayout.BeginArea(new Rect(10, 10, 350, 250));
            GUILayout.Label("🎥 ОТЛАДКА WEBRTC");
            
            if (GUILayout.Button("F1: Проверить статус WebRTC"))
            {
                CheckWebRTCStatus();
            }
            
            if (GUILayout.Button("F2: Подробная статистика"))
            {
                LogDetailedWebRTCStats();
            }
            
            if (GUILayout.Button("F3: Сбросить статистику"))
            {
                ResetWebRTCStats();
            }
            
            if (GUILayout.Button("F4: Запустить WebRTC"))
            {
                StartWebRTCConnection();
            }
            
            if (GUILayout.Button("F5: Остановить WebRTC"))
            {
                StopWebRTCConnection();
            }
            
            // Быстрый статус
            if (_videoService != null)
            {
                GUILayout.Space(10);
                GUILayout.Label($"Подключено: {(_videoService.IsConnected ? "✅" : "❌")} | " +
                              $"Стриминг: {(_videoService.IsStreaming ? "✅" : "❌")}");
                GUILayout.Label($"FPS: {_videoService.CurrentFPS:F1} | " +
                              $"Состояние: {_videoService.ConnectionState}");
                GUILayout.Label($"Кадров: {_videoService.ReceivedFrames} | " +
                              $"Байт: {_videoService.BytesReceived:N0}");
            }
            else
            {
                GUILayout.Space(10);
                GUILayout.Label("❌ WebRTC сервис не найден");
            }
            
            GUILayout.EndArea();
        }
    }
} 