using UnityEngine;
using Scripts.Services;

namespace Scripts.Utils
{
    public class VideoDebugCommands : MonoBehaviour
    {
        private IOptimizedRobotVideoService _videoService;
        
        private void Start()
        {
            FindVideoService();
        }
        
        private void FindVideoService()
        {
            var videoServiceObj = GameObject.FindObjectOfType<OptimizedRobotVideoService>();
            if (videoServiceObj != null)
            {
                _videoService = videoServiceObj;
                Debug.Log("VideoDebugCommands: Сервис видео найден");
            }
        }
        
        private void Update()
        {
            // Горячие клавиши для отладки
            if (Input.GetKeyDown(KeyCode.F1))
            {
                CheckVideoStatus();
            }
            
            if (Input.GetKeyDown(KeyCode.F2))
            {
                LogDetailedVideoStats();
            }
            
            if (Input.GetKeyDown(KeyCode.F3))
            {
                ResetVideoStats();
            }
            
            if (Input.GetKeyDown(KeyCode.F4))
            {
                RequestVideoStream();
            }
        }
        
        [ContextMenu("Check Video Status")]
        public void CheckVideoStatus()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService == null)
            {
                Debug.LogError("❌ Видео сервис не найден!");
                return;
            }
            
            bool videoReceived;
            int totalMessages, videoMessages, invalidFrames;
            _videoService.GetDetailedVideoStats(out videoReceived, out totalMessages, out videoMessages, out invalidFrames);
            
            string status = $"🔍 БЫСТРАЯ ПРОВЕРКА ВИДЕО:\n" +
                          $"• Видео получено: {(videoReceived ? "✅ ДА" : "❌ НЕТ")}\n" +
                          $"• FPS: {_videoService.CurrentFPS:F1}\n" +
                          $"• Кадров обработано: {_videoService.ReceivedFrames}\n" +
                          $"• Всего сообщений: {totalMessages}\n" +
                          $"• Видео сообщений: {videoMessages}";
            
            Debug.Log(status);
            
            if (!videoReceived && totalMessages > 0)
            {
                Debug.LogWarning("⚠️ ПРОБЛЕМА: Сообщения получаем, но видео НЕТ!");
            }
            else if (!videoReceived)
            {
                Debug.LogWarning("⚠️ Видео не получено. Проверьте подключение робота.");
            }
        }
        
        [ContextMenu("Log Detailed Video Stats")]
        public void LogDetailedVideoStats()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                string report = _videoService.GetVideoStatusReport();
                Debug.Log("📊 ПОДРОБНЫЙ ОТЧЕТ О ВИДЕО:\n" + report);
                
                _videoService.ForceVideoStatusLog();
            }
            else
            {
                Debug.LogError("❌ Видео сервис не найден!");
            }
        }
        
        [ContextMenu("Reset Video Stats")]
        public void ResetVideoStats()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                _videoService.ResetStats();
                Debug.Log("🔄 Статистика видео сброшена");
            }
            else
            {
                Debug.LogError("❌ Видео сервис не найден!");
            }
        }
        
        [ContextMenu("Request Video Stream")]
        public void RequestVideoStream()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                _videoService.RequestVideoStream();
                Debug.Log("📺 Запрос видео потока отправлен");
            }
            else
            {
                Debug.LogError("❌ Видео сервис не найден!");
            }
        }
        
        [ContextMenu("Stop Video Stream")]
        public void StopVideoStream()
        {
            if (_videoService == null)
            {
                FindVideoService();
            }
            
            if (_videoService != null)
            {
                _videoService.StopVideoStream();
                Debug.Log("⏹️ Остановка видео потока");
            }
            else
            {
                Debug.LogError("❌ Видео сервис не найден!");
            }
        }
        
        private void OnGUI()
        {
            // Простой GUI для отладки
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("🎥 ОТЛАДКА ВИДЕО");
            
            if (GUILayout.Button("F1: Проверить статус видео"))
            {
                CheckVideoStatus();
            }
            
            if (GUILayout.Button("F2: Подробная статистика"))
            {
                LogDetailedVideoStats();
            }
            
            if (GUILayout.Button("F3: Сбросить статистику"))
            {
                ResetVideoStats();
            }
            
            if (GUILayout.Button("F4: Запросить видео"))
            {
                RequestVideoStream();
            }
            
            // Быстрый статус
            if (_videoService != null)
            {
                GUILayout.Space(10);
                GUILayout.Label($"Видео: {(_videoService.VideoReceived ? "✅" : "❌")} | FPS: {_videoService.CurrentFPS:F1}");
                GUILayout.Label($"Кадров: {_videoService.ReceivedFrames} | Сообщений: {_videoService.TotalMessages}");
            }
            
            GUILayout.EndArea();
        }
    }
} 