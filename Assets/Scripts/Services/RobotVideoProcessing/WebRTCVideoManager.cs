// using UnityEngine;
// using UnityEngine.UI;
// using System;
// using Scripts.Services;

// namespace Scripts.Services.RobotVideoProcessing
// {
//     /// <summary>
//     /// Менеджер WebRTC видео для Unity без зависимости от Unity.WebRTC пакета
//     /// Обрабатывает сигналинг и подготавливает интерфейс для отображения видео
//     /// </summary>
//     public class WebRTCVideoManager : MonoBehaviour, IWebRTCVideoService
//     {
//         [Header("UI Components")]
//         [SerializeField] private RawImage videoDisplay;
//         [SerializeField] private Button requestVideoButton;
//         [SerializeField] private Text statusText;
//         [SerializeField] private Text connectionStatusText;

//         [Header("Video Settings")]
//         [SerializeField] private int videoWidth = 640;
//         [SerializeField] private int videoHeight = 480;
//         [SerializeField] private int targetFPS = 30;

//         // Сервисы и компоненты
//         private IWebSocketClient _webSocketClient;
//         private string _currentSessionId;
//         private bool _isInitialized = false;
//         private bool _isConnected = false;
//         private bool _videoRequested = false;

//         // События
//         public event Action<bool> OnConnectionStateChanged;
//         public event Action<RenderTexture> OnVideoTextureReady;
//         public event Action<string> OnError;

//         // Свойства
//         public bool IsConnected => _isConnected;
//         public RenderTexture VideoTexture { get; private set; }

//         private void Awake()
//         {
//             // Создаем заглушку видео текстуры
//             VideoTexture = new RenderTexture(videoWidth, videoHeight, 0, RenderTextureFormat.BGRA32);
//             VideoTexture.Create();

//             // Создаем тестовый паттерн
//             CreateTestPattern();
//         }

//         private void Start()
//         {
//             // Настройка UI
//             if (requestVideoButton != null)
//             {
//                 requestVideoButton.onClick.AddListener(RequestVideoStream);
//             }

//             UpdateStatus("WebRTC Video Manager инициализирован");
//         }

//         /// <summary>
//         /// Инициализация с WebSocket клиентом
//         /// </summary>
//         public bool Initialize()
//         {
//             try
//             {
//                 if (_isInitialized)
//                 {
//                     Debug.LogWarning("WebRTC Video Manager уже инициализирован");
//                     return true;
//                 }

//                 Debug.Log("🚀 Инициализация WebRTC Video Manager...");

//                 // Получаем WebSocket клиент из системы
//                 var webSocketService = FindObjectOfType<MonoBehaviour>()?.GetComponent<IWebSocketClient>();
//                 if (webSocketService == null)
//                 {
//                     Debug.LogError("❌ WebSocket клиент не найден");
//                     return false;
//                 }

//                 _webSocketClient = webSocketService;
//                 _webSocketClient.OnMessageReceived += HandleWebSocketMessage;

//                 // Настраиваем видео дисплей
//                 if (videoDisplay != null)
//                 {
//                     videoDisplay.texture = VideoTexture;
//                 }

//                 _isInitialized = true;
//                 UpdateStatus("✅ WebRTC инициализирован");
//                 Debug.Log("✅ WebRTC Video Manager инициализирован успешно");

//                 return true;
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка инициализации WebRTC: {ex.Message}");
//                 OnError?.Invoke(ex.Message);
//                 UpdateStatus($"❌ Ошибка: {ex.Message}");
//                 return false;
//             }
//         }

//         /// <summary>
//         /// Инициализация с конкретным WebSocket клиентом
//         /// </summary>
//         public void Initialize(IWebSocketClient webSocketClient)
//         {
//             _webSocketClient = webSocketClient;
//             _webSocketClient.OnMessageReceived += HandleWebSocketMessage;
//             _isInitialized = true;
//             UpdateStatus("✅ WebRTC инициализирован с WebSocket клиентом");
//         }

//         /// <summary>
//         /// Запрос видео потока от робота
//         /// </summary>
//         public void RequestVideoStream()
//         {
//             try
//             {
//                 if (!_isInitialized)
//                 {
//                     Debug.LogError("WebRTC не инициализирован");
//                     UpdateStatus("❌ WebRTC не инициализирован");
//                     return;
//                 }

//                 if (_webSocketClient == null || !_webSocketClient.IsConnected)
//                 {
//                     Debug.LogError("WebSocket не подключен");
//                     UpdateStatus("❌ WebSocket не подключен");
//                     return;
//                 }

//                 Debug.Log("📹 Запрос видео потока от робота...");
//                 UpdateStatus("📹 Запрашиваем видео поток...");

//                 _webSocketClient.SendMessage("REQUEST_VIDEO");
//                 _videoRequested = true;

//                 // Обновляем UI
//                 if (requestVideoButton != null)
//                 {
//                     requestVideoButton.interactable = false;
//                     requestVideoButton.GetComponentInChildren<Text>().text = "Ожидание...";
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка запроса видео: {ex.Message}");
//                 OnError?.Invoke(ex.Message);
//                 UpdateStatus($"❌ Ошибка запроса: {ex.Message}");
//             }
//         }

//         /// <summary>
//         /// Остановка видео потока
//         /// </summary>
//         public void StopVideoStream()
//         {
//             try
//             {
//                 Debug.Log("🛑 Остановка видео потока...");
//                 UpdateStatus("🛑 Остановка видео потока...");

//                 _isConnected = false;
//                 _videoRequested = false;
//                 _currentSessionId = null;

//                 OnConnectionStateChanged?.Invoke(false);

//                 // Обновляем UI
//                 if (requestVideoButton != null)
//                 {
//                     requestVideoButton.interactable = true;
//                     requestVideoButton.GetComponentInChildren<Text>().text = "Запросить видео";
//                 }

//                 UpdateStatus("✅ Видео поток остановлен");
//                 Debug.Log("✅ Видео поток остановлен");
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка остановки видео потока: {ex.Message}");
//             }
//         }

//         /// <summary>
//         /// Обработка WebSocket сообщений
//         /// </summary>
//         private void HandleWebSocketMessage(string message)
//         {
//             try
//             {
//                 Debug.Log($"📨 Получено сообщение: {message}");

//                 // Простая проверка JSON сообщений
//                 if (message.StartsWith("{") && message.Contains("webrtc-signal"))
//                 {
//                     HandleWebRTCMessage(message);
//                 }
//                 else if (message == "VIDEO_STREAM_READY")
//                 {
//                     HandleVideoStreamReady();
//                 }
//                 else if (message.StartsWith("VIDEO_ERROR"))
//                 {
//                     HandleVideoError(message);
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка обработки WebSocket сообщения: {ex.Message}");
//             }
//         }

//         /// <summary>
//         /// Обработка WebRTC сообщений
//         /// </summary>
//         private void HandleWebRTCMessage(string jsonMessage)
//         {
//             try
//             {
//                 // Парсинг WebRTC сигналов через JsonUtility
//                 var baseMessage = JsonUtility.FromJson<WebRTCMessage>(jsonMessage);
                
//                 if (baseMessage == null)
//                 {
//                     Debug.LogWarning("Не удалось распарсить WebRTC сообщение");
//                     return;
//                 }

//                 Debug.Log($"📡 WebRTC сигнал: {baseMessage.signalType} для сессии: {baseMessage.sessionId}");

//                 switch (baseMessage.signalType)
//                 {
//                     case "offer":
//                         HandleWebRTCOffer(jsonMessage);
//                         break;
//                     case "ice-candidate":
//                         HandleWebRTCIceCandidate(jsonMessage);
//                         break;
//                     default:
//                         Debug.Log($"❓ Неизвестный WebRTC сигнал: {baseMessage.signalType}");
//                         break;
//                 }
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка обработки WebRTC сообщения: {ex.Message}");
//             }
//         }

//         /// <summary>
//         /// Обработка WebRTC offer
//         /// </summary>
//         private void HandleWebRTCOffer(string jsonMessage)
//         {
//             try
//             {
//                 var offerMessage = JsonUtility.FromJson<WebRTCOfferMessage>(jsonMessage);
//                 if (offerMessage?.data == null)
//                 {
//                     Debug.LogError("Невалидный WebRTC offer");
//                     return;
//                 }

//                 Debug.Log($"📡 Получен WebRTC offer от робота (сессия: {offerMessage.sessionId})");
//                 _currentSessionId = offerMessage.sessionId;

//                 UpdateStatus($"📡 Получен WebRTC offer (сессия: {_currentSessionId})");

//                 // Создаем answer (в реальной реализации здесь будет WebRTC peer connection)
//                 SendWebRTCAnswer();
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка обработки WebRTC offer: {ex.Message}");
//                 OnError?.Invoke(ex.Message);
//             }
//         }

//         /// <summary>
//         /// Отправка WebRTC answer
//         /// </summary>
//         private void SendWebRTCAnswer()
//         {
//             try
//             {
//                 var answerMessage = new WebRTCAnswerMessage
//                 {
//                     type = "webrtc-signal",
//                     signalType = "answer",
//                     sessionId = _currentSessionId,
//                     data = new WebRTCAnswerData
//                     {
//                         sdp = "v=0\r\no=- 0 0 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\n", // Заглушка SDP
//                         type = "answer",
//                         sessionId = _currentSessionId
//                     }
//                 };

//                 _webSocketClient.SendJsonMessage(answerMessage);
//                 Debug.Log("📤 WebRTC answer отправлен роботу");
//                 UpdateStatus("📤 WebRTC answer отправлен");

//                 // Симулируем установление соединения
//                 Invoke(nameof(SimulateConnectionEstablished), 2.0f);
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка отправки answer: {ex.Message}");
//             }
//         }

//         /// <summary>
//         /// Обработка ICE кандидата
//         /// </summary>
//         private void HandleWebRTCIceCandidate(string jsonMessage)
//         {
//             try
//             {
//                 var candidateMessage = JsonUtility.FromJson<WebRTCIceCandidateMessage>(jsonMessage);
//                 if (candidateMessage?.data == null)
//                 {
//                     Debug.LogError("Невалидный ICE кандидат");
//                     return;
//                 }

//                 Debug.Log($"🧊 Получен ICE кандидат: {candidateMessage.data.candidate}");
//                 UpdateStatus($"🧊 Получен ICE кандидат");

//                 // В реальной реализации здесь добавляется ICE кандидат к peer connection
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Ошибка обработки ICE кандидата: {ex.Message}");
//             }
//         }

//         /// <summary>
//         /// Симуляция установления соединения
//         /// </summary>
//         private void SimulateConnectionEstablished()
//         {
//             _isConnected = true;
//             OnConnectionStateChanged?.Invoke(true);
//             UpdateStatus("✅ WebRTC соединение установлено");
//             Debug.Log("✅ WebRTC соединение установлено (симуляция)");

//             // Уведомляем о готовности видео текстуры
//             OnVideoTextureReady?.Invoke(VideoTexture);
//         }

//         /// <summary>
//         /// Обработка готовности видео потока
//         /// </summary>
//         private void HandleVideoStreamReady()
//         {
//             Debug.Log("✅ Видео поток готов");
//             UpdateStatus("✅ Видео поток готов");
//             _isConnected = true;
//             OnConnectionStateChanged?.Invoke(true);
//         }

//         /// <summary>
//         /// Обработка ошибки видео
//         /// </summary>
//         private void HandleVideoError(string errorMessage)
//         {
//             Debug.LogError($"❌ Ошибка видео: {errorMessage}");
//             UpdateStatus($"❌ {errorMessage}");
//             OnError?.Invoke(errorMessage);
//         }

//         /// <summary>
//         /// Создание тестового паттерна
//         /// </summary>
//         private void CreateTestPattern()
//         {
//             if (VideoTexture == null) return;

//             RenderTexture currentRT = RenderTexture.active;
//             RenderTexture.active = VideoTexture;

//             GL.Clear(true, true, Color.black);
            
//             RenderTexture.active = currentRT;
//         }

//         /// <summary>
//         /// Обновление статуса в UI
//         /// </summary>
//         private void UpdateStatus(string status)
//         {
//             if (statusText != null)
//             {
//                 statusText.text = status;
//             }

//             if (connectionStatusText != null)
//             {
//                 connectionStatusText.text = _isConnected ? "🟢 Подключено" : "🔴 Отключено";
//                 connectionStatusText.color = _isConnected ? Color.green : Color.red;
//             }
//         }

//         /// <summary>
//         /// Освобождение ресурсов
//         /// </summary>
//         public void Dispose()
//         {
//             if (_webSocketClient != null)
//             {
//                 _webSocketClient.OnMessageReceived -= HandleWebSocketMessage;
//             }

//             StopVideoStream();

//             if (VideoTexture != null)
//             {
//                 VideoTexture.Release();
//                 DestroyImmediate(VideoTexture);
//                 VideoTexture = null;
//             }

//             _isInitialized = false;
//             Debug.Log("🗑️ WebRTC Video Manager очищен");
//         }

//         private void OnDestroy()
//         {
//             Dispose();
//         }
//     }
// } 