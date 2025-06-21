using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Collections.Generic;

namespace Scripts.Services
{
    [Serializable]
    public class VideoFrameData
    {
        public string type;
        public string data;
        public float timestamp;
        public int frame_number;
    }

    public interface IOptimizedRobotVideoService
    {
        bool IsVideoConnected { get; }
        float CurrentFPS { get; }
        int ReceivedFrames { get; }
        
        event Action<bool> OnVideoConnectionChanged;
        event Action<Texture2D> OnVideoFrameReceived;
        
        void Initialize(IWebSocketClient webSocketClient, IStatus status);
        void RequestVideoStream();
        void StopVideoStream();
        void SetVideoOutput(RawImage videoOutput);
    }
    
    public class OptimizedRobotVideoService : MonoBehaviour, IOptimizedRobotVideoService
    {
        [Header("Video Settings")]
        [SerializeField] private RawImage _videoOutput;
        [SerializeField] private bool _showDebugInfo = true;
        [SerializeField] private int _maxTexturePoolSize = 3;
        
        [Header("Performance")]
        [SerializeField] private bool _enableFrameSkipping = true;
        [SerializeField] private float _targetFPS = 15f;
        
        private IWebSocketClient _webSocketClient;
        private IStatus _status;
        private bool _isInitialized = false;
        private bool _isVideoStreaming = false;
        
        // Текстуры и декодирование
        private Queue<Texture2D> _texturePool = new Queue<Texture2D>();
        private Texture2D _currentTexture;
        private Coroutine _videoUpdateCoroutine;
        
        // Статистика
        private int _receivedFrames = 0;
        private float _lastFrameTime = 0f;
        private float _currentFPS = 0f;
        private int _fpsCounter = 0;
        private float _lastFPSTime = 0f;
        private Queue<float> _frameTimes = new Queue<float>();
        private int _droppedFrames = 0;
        
        // События
        public event Action<bool> OnVideoConnectionChanged;
        public event Action<Texture2D> OnVideoFrameReceived;
        
        // Свойства
        public bool IsVideoConnected => _isVideoStreaming;
        public float CurrentFPS => _currentFPS;
        public int ReceivedFrames => _receivedFrames;
        
        public void Initialize(IWebSocketClient webSocketClient, IStatus status)
        {
            if (_isInitialized) return;

            _webSocketClient = webSocketClient ?? throw new ArgumentNullException(nameof(webSocketClient));
            _status = status ?? throw new ArgumentNullException(nameof(status));

            _webSocketClient.OnMessageReceived += OnWebSocketMessage;
            
            // Инициализация пула текстур
            InitializeTexturePool();
            
            // Валидация UI компонента
            if (_videoOutput == null)
            {
                _status.Error("КРИТИЧЕСКАЯ ОШИБКА: RawImage не назначен!");
                Debug.LogError("OptimizedRobotVideoService: _videoOutput is null!");
            }
            else
            {
                _status.Info($"RawImage найден: {_videoOutput.name}");
                SetupVideoOutput();
            }
            
            _isInitialized = true;
            _status.Info("OptimizedRobotVideoService инициализирован");
        }
        
        public void SetVideoOutput(RawImage videoOutput)
        {
            _videoOutput = videoOutput;
            if (_videoOutput != null)
            {
                SetupVideoOutput();
            }
        }
        
        private void SetupVideoOutput()
        {
            if (_videoOutput != null)
            {
                var size = _videoOutput.rectTransform.sizeDelta;
                if (size.x == 0 || size.y == 0)
                {
                    _videoOutput.rectTransform.sizeDelta = new Vector2(640, 480);
                    _status.Info("Размер RawImage установлен на 640x480");
                }
            }
        }
        
        private void InitializeTexturePool()
        {
            // Создаем небольшой пул текстур для оптимизации
            for (int i = 0; i < _maxTexturePoolSize; i++)
            {
                var texture = new Texture2D(640, 480, TextureFormat.RGB24, false);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                _texturePool.Enqueue(texture);
            }
            
            Debug.Log($"Инициализирован пул текстур: {_maxTexturePoolSize} текстур");
        }
        
        public void RequestVideoStream()
        {
            if (_webSocketClient != null)
            {
                _webSocketClient.SendMessage("REQUEST_VIDEO_STREAM");
                _status.Info("Запрос видео потока отправлен");
                
                // Запускаем корутину обновления видео
                if (_videoUpdateCoroutine == null)
                {
                    _videoUpdateCoroutine = StartCoroutine(VideoUpdateLoop());
                }
            }
        }
        
        public void StopVideoStream()
        {
            if (_webSocketClient != null)
            {
                _webSocketClient.SendMessage("STOP_VIDEO_STREAM");
                _status.Info("Остановка видео потока");
            }
            
            _isVideoStreaming = false;
            OnVideoConnectionChanged?.Invoke(false);
            
            // Останавливаем корутину
            if (_videoUpdateCoroutine != null)
            {
                StopCoroutine(_videoUpdateCoroutine);
                _videoUpdateCoroutine = null;
            }
        }
        
        private void OnWebSocketMessage(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;
                
                // Обрабатываем видео кадры
                if (message.StartsWith("VIDEO_FRAME!"))
                {
                    string jsonData = message.Substring(12); // Убираем "VIDEO_FRAME!"
                    HandleVideoFrame(jsonData);
                }
                // Обрабатываем регистрацию контроллера
                else if (message == "REGISTERED!CONTROLLER")
                {
                    _status.Info("Контроллер зарегистрирован для видео");
                    
                    // Автоматически запрашиваем видео через небольшую задержку
                    Invoke(nameof(RequestVideoStream), 1f);
                }
                // Обрабатываем статус видео потока
                else if (message == "VIDEO_STREAM_STARTED")
                {
                    _status.Info("Видео поток запущен на роботе");
                }
                else if (message == "VIDEO_STREAM_STOPPED")
                {
                    _status.Info("Видео поток остановлен на роботе");
                    _isVideoStreaming = false;
                    OnVideoConnectionChanged?.Invoke(false);
                }
                // Обрабатываем ошибки видео
                else if (message.StartsWith("VIDEO_ERROR!"))
                {
                    string errorMsg = message.Substring(12);
                    _status.Error($"Ошибка видео: {errorMsg}");
                }
                
            }
            catch (Exception ex)
            {
                _status.Error($"Ошибка обработки сообщения: {ex.Message}");
                Debug.LogError($"OptimizedRobotVideoService error: {ex}");
            }
        }
        
        private void HandleVideoFrame(string jsonData)
        {
            try
            {
                var frameData = JsonUtility.FromJson<VideoFrameData>(jsonData);
                
                if (frameData != null && !string.IsNullOrEmpty(frameData.data))
                {
                    // Обновляем статистику
                    UpdateFrameStats();
                    
                    // Проверяем нужно ли пропустить кадр для оптимизации
                    if (_enableFrameSkipping && ShouldSkipFrame())
                    {
                        _droppedFrames++;
                        return;
                    }
                    
                    // Декодируем кадр
                    DecodeAndDisplayFrame(frameData);
                    
                    if (!_isVideoStreaming)
                    {
                        _isVideoStreaming = true;
                        OnVideoConnectionChanged?.Invoke(true);
                        _status.Info("Видео поток активен");
                    }
                }
            }
            catch (Exception ex)
            {
                _status.Error($"Ошибка декодирования видео кадра: {ex.Message}");
                Debug.LogError($"Video frame decode error: {ex}");
            }
        }
        
        private bool ShouldSkipFrame()
        {
            // Пропускаем кадры если FPS слишком высокий
            float currentTime = Time.time;
            float targetInterval = 1f / _targetFPS;
            
            if (currentTime - _lastFrameTime < targetInterval)
            {
                return true;
            }
            
            _lastFrameTime = currentTime;
            return false;
        }
        
        private void DecodeAndDisplayFrame(VideoFrameData frameData)
        {
            try
            {
                // Декодируем base64 в байты
                byte[] imageData = Convert.FromBase64String(frameData.data);
                
                // Получаем текстуру из пула или создаем новую
                Texture2D texture = GetTextureFromPool();
                
                // Загружаем JPEG данные
                if (texture.LoadImage(imageData))
                {
                    // Обновляем UI
                    if (_videoOutput != null)
                    {
                        // Освобождаем предыдущую текстуру
                        if (_currentTexture != null)
                        {
                            ReturnTextureToPool(_currentTexture);
                        }
                        
                        _currentTexture = texture;
                        _videoOutput.texture = texture;
                        
                        // Уведомляем о новом кадре
                        OnVideoFrameReceived?.Invoke(texture);
                    }
                    else
                    {
                        ReturnTextureToPool(texture);
                    }
                }
                else
                {
                    _status.Error("Не удалось загрузить изображение");
                    ReturnTextureToPool(texture);
                }
            }
            catch (Exception ex)
            {
                _status.Error($"Ошибка декодирования изображения: {ex.Message}");
                Debug.LogError($"Image decode error: {ex}");
            }
        }
        
        private Texture2D GetTextureFromPool()
        {
            if (_texturePool.Count > 0)
            {
                return _texturePool.Dequeue();
            }
            else
            {
                // Создаем новую текстуру если пул пуст
                var texture = new Texture2D(640, 480, TextureFormat.RGB24, false);
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                return texture;
            }
        }
        
        private void ReturnTextureToPool(Texture2D texture)
        {
            if (texture != null && _texturePool.Count < _maxTexturePoolSize)
            {
                _texturePool.Enqueue(texture);
            }
        }
        
        private void UpdateFrameStats()
        {
            _receivedFrames++;
            _fpsCounter++;
            
            float currentTime = Time.time;
            
            // Обновляем FPS каждую секунду
            if (currentTime - _lastFPSTime >= 1f)
            {
                _currentFPS = _fpsCounter / (currentTime - _lastFPSTime);
                _fpsCounter = 0;
                _lastFPSTime = currentTime;
                
                // Логируем статистику
                if (_showDebugInfo)
                {
                    Debug.Log($"Video FPS: {_currentFPS:F1}, Frames: {_receivedFrames}, Dropped: {_droppedFrames}");
                }
            }
            
            // Отслеживаем время между кадрами
            _frameTimes.Enqueue(currentTime);
            while (_frameTimes.Count > 30) // Сохраняем последние 30 кадров
            {
                _frameTimes.Dequeue();
            }
        }
        
        private IEnumerator VideoUpdateLoop()
        {
            while (true)
            {
                // Мониторинг состояния видео
                if (_isVideoStreaming)
                {
                    float timeSinceLastFrame = Time.time - _lastFrameTime;
                    
                    // Если давно не было кадров - считаем что соединение потеряно
                    if (timeSinceLastFrame > 5f)
                    {
                        _status.Warning("Видео поток прерван - нет кадров более 5 секунд");
                        _isVideoStreaming = false;
                        OnVideoConnectionChanged?.Invoke(false);
                    }
                }
                
                yield return new WaitForSeconds(1f);
            }
        }
        
        private void OnDestroy()
        {
            if (_videoUpdateCoroutine != null)
            {
                StopCoroutine(_videoUpdateCoroutine);
            }
            
            // Освобождаем текстуры
            if (_currentTexture != null)
            {
                Destroy(_currentTexture);
            }
            
            while (_texturePool.Count > 0)
            {
                var texture = _texturePool.Dequeue();
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
            
            if (_webSocketClient != null)
            {
                _webSocketClient.OnMessageReceived -= OnWebSocketMessage;
            }
        }
        
        // Публичные методы для мониторинга
        public void GetVideoStats(out float fps, out int frames, out int dropped)
        {
            fps = _currentFPS;
            frames = _receivedFrames;
            dropped = _droppedFrames;
        }
        
        public void ResetStats()
        {
            _receivedFrames = 0;
            _droppedFrames = 0;
            _fpsCounter = 0;
            _lastFPSTime = Time.time;
            _frameTimes.Clear();
        }
        
        // Методы для настройки производительности
        public void SetTargetFPS(float targetFPS)
        {
            _targetFPS = Mathf.Clamp(targetFPS, 1f, 60f);
        }
        
        public void SetFrameSkipping(bool enabled)
        {
            _enableFrameSkipping = enabled;
        }
        
        public void SetTexturePoolSize(int size)
        {
            _maxTexturePoolSize = Mathf.Clamp(size, 1, 10);
        }
    }
} 