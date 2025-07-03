using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scripts.Services;

namespace Scripts.UI
{
    public class VideoStatusDisplay : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Button _refreshButton;
        [SerializeField] private Button _resetStatsButton;
        [SerializeField] private Toggle _autoRefreshToggle;
        
        [Header("Settings")]
        [SerializeField] private float _refreshInterval = 2f;
        [SerializeField] private bool _showDetailedStats = true;
        
        private IWebRTCVideoService _videoService;
        private float _lastRefreshTime;
        private bool _autoRefresh = true;
        
        private void Start()
        {
            SetupUI();
            FindVideoService();
        }
        
        private void SetupUI()
        {
            if (_refreshButton != null)
            {
                _refreshButton.onClick.AddListener(RefreshStatus);
            }
            
            if (_resetStatsButton != null)
            {
                _resetStatsButton.onClick.AddListener(ResetStats);
            }
            
            if (_autoRefreshToggle != null)
            {
                _autoRefreshToggle.isOn = _autoRefresh;
                _autoRefreshToggle.onValueChanged.AddListener(SetAutoRefresh);
            }
            
            if (_statusText == null)
            {
                Debug.LogError("VideoStatusDisplay: StatusText не назначен!");
            }
        }
        
        private void FindVideoService()
        {
            // Попытка найти WebRTC видео сервис
            var videoServiceObj = GameObject.FindObjectOfType<WebRTCVideoService>();
            if (videoServiceObj != null)
            {
                _videoService = videoServiceObj;
                Debug.Log("VideoStatusDisplay: Найден WebRTCVideoService");
            }
            else
            {
                Debug.LogWarning("VideoStatusDisplay: WebRTCVideoService не найден!");
            }
        }
        
        private void Update()
        {
            if (_autoRefresh && Time.time - _lastRefreshTime >= _refreshInterval)
            {
                RefreshStatus();
            }
        }
        
        private void RefreshStatus()
        {
            _lastRefreshTime = Time.time;
            
            if (_videoService == null)
            {
                FindVideoService();
                if (_videoService == null)
                {
                    UpdateStatusText("❌ WebRTC видео сервис не найден!");
                    return;
                }
            }
            
            if (_showDetailedStats)
            {
                string detailedStatus = _videoService.GetStatusReport();
                UpdateStatusText(detailedStatus);
            }
            else
            {
                string basicStatus = GetBasicStatus();
                UpdateStatusText(basicStatus);
            }
        }
        
        private string GetBasicStatus()
        {
            if (_videoService == null) return "Сервис не найден";
            
            return $"🎥 WebRTC: {(_videoService.IsConnected ? "✅ Подключено" : "❌ Отключено")}\n" +
                   $"📺 Стриминг: {(_videoService.IsStreaming ? "✅ Активен" : "❌ Неактивен")}\n" +
                   $"📊 FPS: {_videoService.CurrentFPS:F1}\n" +
                   $"🎬 Кадров: {_videoService.ReceivedFrames}\n" +
                   $"📏 Байт: {_videoService.BytesReceived:N0}";
        }
        
        private void UpdateStatusText(string status)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }
        }
        
        private void ResetStats()
        {
            if (_videoService != null)
            {
                _videoService.ResetStatistics();
                RefreshStatus();
                Debug.Log("Статистика WebRTC сброшена");
            }
        }
        
        private void SetAutoRefresh(bool enabled)
        {
            _autoRefresh = enabled;
        }
        
        public void SetVideoService(IWebRTCVideoService videoService)
        {
            _videoService = videoService;
        }
        
        public void SetRefreshInterval(float interval)
        {
            _refreshInterval = Mathf.Clamp(interval, 0.5f, 10f);
        }
        
        public void ToggleDetailedStats()
        {
            _showDetailedStats = !_showDetailedStats;
            RefreshStatus();
        }
        
        public void StartConnection()
        {
            if (_videoService != null)
            {
                _videoService.StartConnection();
                Debug.Log("Запуск WebRTC соединения...");
            }
        }
        
        public void StopConnection()
        {
            if (_videoService != null)
            {
                _videoService.StopConnection();
                Debug.Log("Остановка WebRTC соединения...");
            }
        }
        
        private void OnDestroy()
        {
            if (_refreshButton != null)
            {
                _refreshButton.onClick.RemoveAllListeners();
            }
            
            if (_resetStatsButton != null)
            {
                _resetStatsButton.onClick.RemoveAllListeners();
            }
            
            if (_autoRefreshToggle != null)
            {
                _autoRefreshToggle.onValueChanged.RemoveAllListeners();
            }
        }
    }
} 