using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using Unity.WebRTC;
using System.Collections.Generic;
using System.Linq;

namespace Scripts.Services
{
    public class WebRTCVideoService : MonoBehaviour, IWebRTCVideoService
    {
        [Header("WebRTC Settings")]
        [SerializeField] private RawImage _videoOutput;
        [SerializeField] private bool _enableDebugLogs = true;        
        
        private IWebSocketClient _webSocketClient;
        private bool _isInitialized = false;
        private bool _isConnected = false;
        private bool _isStreaming = false;
        private string _connectionState = "Disconnected";
        private string _currentSessionId = "";
        
        // Unity WebRTC компоненты
        private RTCPeerConnection _peerConnection;
        private RTCConfiguration _rtcConfiguration;
        private MediaStream _remoteStream;
        private VideoStreamTrack _remoteVideoTrack;
        
        // TURN/ICE конфигурация
        private ICEServerConfiguration _iceConfiguration;
        private bool _iceConfigurationLoaded = false;
        
        // Статистика
        private float _currentFPS = 0f;
        private int _receivedFrames = 0;
        private long _bytesReceived = 0;
        private DateTime _connectionStartTime;
        private DateTime _lastFrameTime;
        private int _fpsCounter = 0;
        private float _lastFpsTime = 0f;
        
        // Диагностика сообщений (упрощенная)
        private int _webrtcSignalsReceived = 0;
        private int _offersReceived = 0;
        
        // События
        public event Action<bool> OnConnectionStateChanged;
        public event Action<string> OnError;
        
        // Свойства
        public bool IsConnected => _isConnected;
        public bool IsStreaming => _isStreaming;
        public string ConnectionState => _connectionState;
        public float CurrentFPS => _currentFPS;
        public int ReceivedFrames => _receivedFrames;
        public long BytesReceived => _bytesReceived;
        public TimeSpan ConnectionTime => _isConnected ? DateTime.Now - _connectionStartTime : TimeSpan.Zero;
        public string CurrentSessionId => _currentSessionId;
        
        
        public void Initialize(IWebSocketClient webSocketClient)
        {
            if (_isInitialized) return;
            
            _webSocketClient = webSocketClient ?? throw new ArgumentNullException(nameof(webSocketClient));
            
            // Подписываемся на WebSocket сообщения для WebRTC сигналинга
            _webSocketClient.OnMessageReceived += OnWebSocketMessage;
            
            _isInitialized = true;
            LogDebug("WebRTC видео сервис инициализирован с новой архитектурой TURN/сессий");
        }
        
        public async void StartConnection()
        {
            if (!_isInitialized)
            {
                LogError("Сервис не инициализирован!");
                return;
            }
            
            LogDebug("Запуск WebRTC соединения...");
            
            // Запрашиваем видео - ICE конфигурация придет в ответе
            RequestVideoStream();
            
            _connectionState = "Connecting";
            _connectionStartTime = DateTime.Now;
            OnConnectionStateChanged?.Invoke(false);
        }
        

        
        private void SetupFallbackICEConfiguration()
        {
            LogDebug("⚠️ Используем fallback ICE конфигурацию (только STUN)");
            _iceConfiguration = new ICEServerConfiguration
            {
                iceServers = new ICEServer[]
                {
                    new ICEServer { urls = "stun:stun.l.google.com:19302", username = "", credential = "" },
                    new ICEServer { urls = "stun:stun1.l.google.com:19302", username = "", credential = "" }
                },
                iceCandidatePoolSize = 10,
                bundlePolicy = "max-bundle",
                rtcpMuxPolicy = "require"
            };
            _iceConfigurationLoaded = true;
        }
        
        private void SetupWebRTCConfiguration()
        {
            if (!_iceConfigurationLoaded || _iceConfiguration?.iceServers == null)
            {
                LogError("ICE конфигурация не загружена!");
                return;
            }
            
            var iceServers = new List<RTCIceServer>();
            
            foreach (var iceServer in _iceConfiguration.iceServers)
            {
                var rtcIceServer = new RTCIceServer
                {
                    urls = new string[] { iceServer.urls }
                };
                
                // Добавляем credentials для TURN серверов
                if (!string.IsNullOrEmpty(iceServer.username) && !string.IsNullOrEmpty(iceServer.credential))
                {
                    rtcIceServer.username = iceServer.username;
                    rtcIceServer.credential = iceServer.credential;
                    LogDebug($"🔐 TURN сервер: {iceServer.urls} с credentials");
                }
                else
                {
                    LogDebug($"📡 STUN сервер: {iceServer.urls}");
                }
                
                iceServers.Add(rtcIceServer);
            }
            
            _rtcConfiguration = default;
            _rtcConfiguration.iceServers = iceServers.ToArray();
            _rtcConfiguration.iceCandidatePoolSize = _iceConfiguration.iceCandidatePoolSize;
            
            LogDebug($"✅ WebRTC конфигурация настроена: {iceServers.Count} ICE серверов");
        }
        
        private void RequestVideoStream()
        {
            // Генерируем уникальный sessionId для Unity контроллера
            _currentSessionId = System.Guid.NewGuid().ToString();
            
            var requestMessage = new WebRTCSignalMessage
            {
                type = "webrtc-signal",
                signalType = "request_video",
                sessionId = _currentSessionId,
                data = "{\"clientType\":\"unity-controller\"}"
            };
            
            var json = JsonUtility.ToJson(requestMessage);
            
            _webSocketClient.SendMessage(json);
            LogDebug($"📡 Отправлен запрос видео с sessionId: {_currentSessionId}");
        }
        
        private void HandleSessionReady(SessionReadyMessage message)
        {
            try
            {
                LogDebug($"🎯 Обработка session_ready для сессии: {message.sessionId}");
                
                // Проверяем, что это наша сессия
                if (message.sessionId != _currentSessionId)
                {
                    LogError($"❌ Получен session_ready для чужой сессии: {message.sessionId} (ожидалась: {_currentSessionId})");
                    return;
                }
                
                // Используем полученную ICE конфигурацию
                _iceConfiguration = message.data.iceConfiguration;
                _iceConfigurationLoaded = true;
                
                LogDebug($"✅ ICE конфигурация получена из session_ready: STUN: {_iceConfiguration?.iceServers?.Count(s => s.urls.Contains("stun:"))} серверов, TURN: {_iceConfiguration?.iceServers?.Count(s => s.urls.Contains("turn:"))} серверов");
                
                // Настраиваем WebRTC конфигурацию с полученными данными
                SetupWebRTCConfiguration();
                
                // Создаем RTCPeerConnection
                CreatePeerConnection();
                
                // Проверяем, что PeerConnection создан успешно
                if (_peerConnection == null)
                {
                    LogError("Не удалось создать PeerConnection. Соединение прервано.");
                    return;
                }
                
                LogDebug($"✅ WebRTC готов к получению offer от робота (session: {_currentSessionId})");
                
                // Логируем информацию о сессии
                if (message.data.sessionInfo != null)
                {
                    LogDebug($"📊 Статус робота: доступен={message.data.sessionInfo.robotAvailable}, камера={message.data.sessionInfo.cameraActive}");
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ Ошибка обработки session_ready: {ex.Message}");
                OnError?.Invoke($"Session ready error: {ex.Message}");
            }
        }
        
        private void CreatePeerConnection()
        {
            try
            {
                if (_peerConnection != null)
                {
                    LogDebug("Disposing existing PeerConnection");
                    _peerConnection.Dispose();
                    _peerConnection = null;
                }
                
                LogDebug("Creating new RTCPeerConnection");
                _peerConnection = new RTCPeerConnection(ref _rtcConfiguration);
                
                if (_peerConnection == null)
                {
                    LogError("Не удалось создать RTCPeerConnection!");
                    return;
                }
                
                // Настройка event handlers
                _peerConnection.OnIceCandidate = OnIceCandidate;
                _peerConnection.OnIceConnectionChange = OnIceConnectionChange;
                _peerConnection.OnConnectionStateChange = OnConnectionStateChange;
                _peerConnection.OnTrack = OnTrack;
                
                LogDebug("✅ RTCPeerConnection создан успешно");
            }
            catch (Exception ex)
            {
                LogError($"Ошибка создания RTCPeerConnection: {ex.Message}");
                _peerConnection = null;
            }
        }
        
        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            LogDebug("Получен ICE candidate");
            
            var candidateData = new IceCandidateData
            {
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = candidate.SdpMLineIndex ?? 0
            };
            
            SendWebRTCSignal("ice-candidate", JsonUtility.ToJson(candidateData));
        }
        
        private void OnIceConnectionChange(RTCIceConnectionState state)
        {
            LogDebug($"ICE Connection State: {state}");
            
            switch (state)
            {
                case RTCIceConnectionState.Connected:
                case RTCIceConnectionState.Completed:
                    _isConnected = true;
                    _connectionState = "Connected";
                    OnConnectionStateChanged?.Invoke(true);
                    break;
                case RTCIceConnectionState.Disconnected:
                case RTCIceConnectionState.Failed:
                case RTCIceConnectionState.Closed:
                    _isConnected = false;
                    _isStreaming = false;
                    _connectionState = "Disconnected";
                    OnConnectionStateChanged?.Invoke(false);
                    break;
            }
        }
        
        private void OnConnectionStateChange(RTCPeerConnectionState state)
        {
            LogDebug($"Peer Connection State: {state}");
        }
        
        private void OnTrack(RTCTrackEvent e)
        {
            LogDebug("🎬 === OnTrack вызван ===");
            LogDebug($"🎬 Track Type: {e.Track?.GetType()?.Name ?? "null"}");
            LogDebug($"🎬 Track Kind: {(e.Track?.Kind.ToString() ?? "null")}");
            LogDebug($"🎬 Streams Count: {e.Streams?.Count() ?? 0}");
            LogDebug($"🎬 VideoOutput Status: {(_videoOutput != null ? $"✅ {_videoOutput.name}" : "❌ null")}");
            
            if (e.Track is VideoStreamTrack videoTrack)
            {
                LogDebug("🎬 ✅ Это видео трек!");
                _remoteVideoTrack = videoTrack;
                _remoteStream = e.Streams.FirstOrDefault();
                
                LogDebug($"🎬 VideoTrack.Texture: {(videoTrack.Texture != null ? $"✅ {videoTrack.Texture.width}x{videoTrack.Texture.height}" : "❌ null (будет создана автоматически при получении кадров)")}");
                
                // Привязываем видео к UI
                if (_videoOutput != null)
                {
                    // Если текстура уже доступна, подключаем сразу
                    if (videoTrack.Texture != null)
                    {
                        _videoOutput.texture = videoTrack.Texture;
                        _isStreaming = true;
                        _connectionState = "Streaming";
                        LogDebug($"🎬 ✅ Текстура доступна сразу! Подключено: {videoTrack.Texture.width}x{videoTrack.Texture.height}");
                    }
                    else
                    {
                        LogDebug("🎬 ⏳ Текстура пока недоступна. Ожидаем первых кадров...");
                        LogDebug("🎬 📺 Update() будет автоматически подключать текстуру когда она станет доступной");
                    }
                }
                else
                {
                    LogError("🎬 ❌ _videoOutput не установлен! Видео не будет отображаться");
                }
            }
            else
            {
                LogDebug($"🎬 ❌ Это не видео трек: {e.Track?.GetType()?.Name}");
            }
        }
        
        public void StopConnection()
        {
            LogDebug("Остановка WebRTC соединения...");
            
            _isConnected = false;
            _isStreaming = false;
            _connectionState = "Disconnected";
            
            // Очистка WebRTC ресурсов
            if (_peerConnection != null)
            {
                _peerConnection.Close();
                _peerConnection.Dispose();
                _peerConnection = null;
            }
            
            if (_videoOutput != null)
            {
                _videoOutput.texture = null;
            }
            
            _remoteVideoTrack = null;
            _remoteStream = null;
            
            OnConnectionStateChanged?.Invoke(false);
        }
        
        public void SetVideoOutput(RawImage videoOutput)
        {
            _videoOutput = videoOutput;
            LogDebug($"Видео выход установлен: {(_videoOutput != null ? _videoOutput.name : "null")}");
        }
        
        public void HandleWebRTCSignal(string signalType, string data)
        {
            LogDebug($"🚀 === HANDLING WebRTC SIGNAL ===");
            LogDebug($"🚀 Signal Type: '{signalType}'");
            LogDebug($"🚀 Data Length: {data?.Length ?? 0} chars");
            LogDebug($"🚀 Data Content: {data}");
            LogDebug($"🚀 PeerConnection Status: {(_peerConnection != null ? "✅ Active" : "❌ Null")}");
            
            if (string.IsNullOrEmpty(signalType))
            {
                LogError("❌ Signal type пуст!");
                return;
            }
            
            switch (signalType)
            {
                case "offer":
                    LogDebug($"🔥 === STARTING OFFER PROCESSING ===");
                    LogDebug($"🔥 Offer #{_offersReceived} processing started");
                    StartCoroutine(HandleOfferCoroutine(data));
                    break;
                case "answer":
                    LogDebug($"📝 === STARTING ANSWER PROCESSING ===");
                    StartCoroutine(HandleAnswerCoroutine(data));
                    break;
                case "ice-candidate":
                    LogDebug($"🧊 === STARTING ICE CANDIDATE PROCESSING ===");
                    HandleIceCandidate(data);
                    break;
                default:
                    LogDebug($"❓ Неизвестный тип WebRTC сигнала: '{signalType}'");
                    break;
            }
        }
        
        private IEnumerator HandleOfferCoroutine(string offerData)
        {
            LogDebug($"🔥 === OFFER COROUTINE STARTED ===");
            LogDebug($"🔥 Offer Data Received: {offerData}");
            LogDebug($"🔥 Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            LogDebug($"🔥 Time: {DateTime.Now:HH:mm:ss.fff}");
            
            // Проверяем PeerConnection
            if (_peerConnection == null)
            {
                LogError("❌ PeerConnection не инициализирован!");
                LogError("❌ Offer processing FAILED - no PeerConnection");
                yield break;
            }
            LogDebug($"✅ PeerConnection is available");
            
            // Проверяем входные данные
            if (string.IsNullOrEmpty(offerData))
            {
                LogError("❌ Offer данные пусты!");
                LogError("❌ Offer processing FAILED - empty offer data");
                yield break;
            }
            LogDebug($"✅ Offer data is not empty ({offerData.Length} chars)");
            
            SdpData offerObj;
            RTCSessionDescription offer;
            
            LogDebug($"🔄 Starting JSON parsing...");
            try
            {
                offerObj = JsonUtility.FromJson<SdpData>(offerData);
                LogDebug($"✅ JSON parsing completed");
                
                if (offerObj == null)
                {
                    LogError("❌ Не удалось распарсить offer JSON - result is null");
                    LogError("❌ Offer processing FAILED - JSON parse returned null");
                    yield break;
                }
                LogDebug($"✅ Parsed SdpData object is not null");
                
                LogDebug($"📋 Parsed offer - type: '{offerObj.type}', sdp length: {offerObj.sdp?.Length ?? 0}");
                
                if (string.IsNullOrEmpty(offerObj.sdp))
                {
                    LogError("❌ SDP в offer пуст");
                    LogError("❌ Offer processing FAILED - SDP is empty");
                    yield break;
                }
                LogDebug($"✅ SDP is not empty");
                
                offer = new RTCSessionDescription
                {
                    type = RTCSdpType.Offer,
                    sdp = offerObj.sdp
                };
                LogDebug($"✅ RTCSessionDescription created successfully");
                
                var sdpPreview = offerObj.sdp.Length > 200 ? offerObj.sdp.Substring(0, 200) + "..." : offerObj.sdp;
                LogDebug($"📄 Offer SDP Preview: {sdpPreview}");
            }
            catch (Exception ex)
            {
                LogError($"💥 Ошибка парсинга offer: {ex.Message}");
                LogError($"💥 Stack trace: {ex.StackTrace}");
                LogError($"💥 Offer data: {offerData}");
                LogError($"💥 Offer processing FAILED - JSON parsing exception");
                yield break;
            }
            
            LogDebug($"🔄 JSON parsing completed successfully, proceeding to setRemoteDescription...");
            LogDebug($"🔄 This is where offer processing continues...");
            
            var setRemoteOp = _peerConnection.SetRemoteDescription(ref offer);
            yield return setRemoteOp;
            
            if (setRemoteOp.IsError)
            {
                LogError($"Ошибка установки remote description: {setRemoteOp.Error.message}");
                yield break;
            }
            
            // Создание answer
            var answerOp = _peerConnection.CreateAnswer();
            yield return answerOp;
            
            if (answerOp.IsError)
            {
                LogError($"Ошибка создания answer: {answerOp.Error.message}");
                yield break;
            }
            
            var answer = answerOp.Desc;
            var setLocalOp = _peerConnection.SetLocalDescription(ref answer);
            yield return setLocalOp;
            
            if (setLocalOp.IsError)
            {
                LogError($"Ошибка установки local description: {setLocalOp.Error.message}");
                yield break;
            }
            
            // Отправка answer
            var answerData = new SdpData
            {
                type = "answer",
                sdp = answer.sdp
            };
            
            SendWebRTCSignal("answer", JsonUtility.ToJson(answerData));
            LogDebug("WebRTC answer отправлен");
        }
        
        private IEnumerator HandleAnswerCoroutine(string answerData)
        {
            LogDebug("Обработка WebRTC answer...");
            
            SdpData answerObj;
            RTCSessionDescription answer;
            
            try
            {
                answerObj = JsonUtility.FromJson<SdpData>(answerData);
                answer = new RTCSessionDescription
                {
                    type = RTCSdpType.Answer,
                    sdp = answerObj.sdp
                };
            }
            catch (Exception ex)
            {
                LogError($"Ошибка парсинга answer: {ex.Message}");
                yield break;
            }
            
            var setRemoteOp = _peerConnection.SetRemoteDescription(ref answer);
            yield return setRemoteOp;
            
            if (setRemoteOp.IsError)
            {
                LogError($"Ошибка установки remote answer: {setRemoteOp.Error.message}");
            }
            else
            {
                LogDebug("WebRTC answer обработан успешно");
            }
        }
        
        private void HandleIceCandidate(string candidateData)
        {
            LogDebug("Обработка ICE candidate...");
            
            try
            {
                var candidateObj = JsonUtility.FromJson<IceCandidateData>(candidateData);
                var candidate = new RTCIceCandidate(new RTCIceCandidateInit
                {
                    candidate = candidateObj.candidate,
                    sdpMid = candidateObj.sdpMid,
                    sdpMLineIndex = candidateObj.sdpMLineIndex
                });
                
                _peerConnection.AddIceCandidate(candidate);
                LogDebug("ICE candidate добавлен");
            }
            catch (Exception ex)
            {
                LogError($"Ошибка обработки ICE candidate: {ex.Message}");
            }
        }
        
        private void Update()
        {
            if (_isStreaming && _remoteVideoTrack != null)
            {
                UpdateFrameStatistics();
            }
            
            // Проверяем доступность текстуры видео
            CheckVideoTextureAvailability();
        }
        
        private void CheckVideoTextureAvailability()
        {
            // Проверяем, доступна ли текстура от видео трека
            if (_remoteVideoTrack != null && _videoOutput != null)
            {
                // Если текстура стала доступной, но еще не подключена к UI
                if (_remoteVideoTrack.Texture != null && _videoOutput.texture != _remoteVideoTrack.Texture)
                {
                    LogDebug($"🎬 ✅ Текстура стала доступной! Подключаем к UI: {_remoteVideoTrack.Texture.width}x{_remoteVideoTrack.Texture.height}");
                    LogDebug($"🎬 📊 Texture type: {_remoteVideoTrack.Texture.GetType().Name}");
                    if (_remoteVideoTrack.Texture is RenderTexture rt)
                    {
                        LogDebug($"🎬 📊 RenderTexture format: {rt.format}");
                    }
                    
                    _videoOutput.texture = _remoteVideoTrack.Texture;
                    _isStreaming = true;
                    _connectionState = "Streaming";
                    LogDebug("🎬 ✅ Видео успешно подключено к RawImage!");
                    
                    // Дополнительная диагностика каждые 5 секунд
                    InvokeRepeating(nameof(LogVideoStatus), 5f, 5f);
                }
            }
        }
        
        private void LogVideoStatus()
        {
            if (_remoteVideoTrack?.Texture != null)
            {
                LogDebug($"🎬 📊 ВИДЕО СТАТУС: Размер={_remoteVideoTrack.Texture.width}x{_remoteVideoTrack.Texture.height}, " +
                        $"FPS={_currentFPS:F1}, Кадров={_receivedFrames}");
                LogDebug($"🎬 📊 Текстура подключена к UI: {(_videoOutput?.texture == _remoteVideoTrack.Texture ? "✅ ДА" : "❌ НЕТ")}");
            }
        }
        
        private void UpdateFrameStatistics()
        {
            _fpsCounter++;
            _receivedFrames++;
            
            if (Time.time - _lastFpsTime >= 1f)
            {
                _currentFPS = _fpsCounter / (Time.time - _lastFpsTime);
                _fpsCounter = 0;
                _lastFpsTime = Time.time;
            }
            
            _lastFrameTime = DateTime.Now;
            
            // Приблизительный подсчет байтов (основан на разрешении видео)
            if (_remoteVideoTrack?.Texture != null)
            {
                var texture = _remoteVideoTrack.Texture;
                _bytesReceived += (long)(texture.width * texture.height * 3); // RGB
            }
        }
        
        private void OnWebSocketMessage(string message)
        {
            try
            {
                LogDebug($"📨 WebSocket Message received");
                LogDebug($"📨 Content: {message}");
                
                if (string.IsNullOrEmpty(message))
                {
                    LogDebug("⚠️ Получено пустое WebSocket сообщение");
                    return;
                }
                
                // Обработка специальных сообщений с префиксами
                if (message.StartsWith("REGISTERED!"))
                {
                    LogDebug($"✅ Получено сообщение регистрации: {message}");
                    return; // Просто игнорируем, регистрация обрабатывается в WebSocketClient
                }
                
                if (message.StartsWith("TELEMETRY!"))
                {
                    string telemetryJson = message.Substring("TELEMETRY!".Length);
                    LogDebug($"📊 Получена телеметрия: {telemetryJson.Substring(0, Math.Min(100, telemetryJson.Length))}...");
                    // Можно добавить обработку телеметрии здесь если нужно
                    return;
                }
                
                // Проверяем, что это JSON сообщение (начинается с '{')
                if (!message.StartsWith("{"))
                {
                    LogDebug($"⚠️ Сообщение не является JSON, игнорируем: {message.Substring(0, Math.Min(50, message.Length))}...");
                    return;
                }
                
                // Сначала парсим базовую структуру
                var basicMessage = JsonUtility.FromJson<BasicWebRTCMessage>(message);
                
                if (basicMessage == null)
                {
                    LogError($"❌ Не удалось распарсить базовое WebSocket сообщение: {message}");
                    return;
                }
                
                LogDebug($"✅ Parsed message - type: '{basicMessage.type}', signalType: '{basicMessage.signalType}'");
                
                if (basicMessage.type == "webrtc_signal" || basicMessage.type == "webrtc-signal")
                {
                    _webrtcSignalsReceived++;
                    LogDebug($"🎯 WebRTC Signal #{_webrtcSignalsReceived}: {basicMessage.signalType}");
                    
                    if (string.IsNullOrEmpty(basicMessage.signalType))
                    {
                        LogError("❌ Signal type пуст в WebRTC сообщении");
                        return;
                    }
                    
                    string dataJson = null;
                    
                    // Парсим данные в зависимости от типа сигнала
                    switch (basicMessage.signalType)
                    {
                        case "offer":
                        case "answer":
                            var offerMessage = JsonUtility.FromJson<WebRTCOfferAnswerMessage>(message);
                            if (offerMessage?.data != null)
                            {
                                dataJson = JsonUtility.ToJson(offerMessage.data);
                                
                                if (basicMessage.signalType == "offer")
                                {
                                    _offersReceived++;
                                    LogDebug($"🔥 OFFER RECEIVED #{_offersReceived}");
                                    LogDebug($"🔥 Offer SDP length: {offerMessage.data.sdp?.Length ?? 0}");
                                }
                                else // answer
                                {
                                    LogDebug($"📝 Answer received");
                                    LogDebug($"📝 Answer SDP length: {offerMessage.data.sdp?.Length ?? 0}");
                                }
                            }
                            else
                            {
                                LogError("❌ Не удалось распарсить offer/answer данные");
                                return;
                            }
                            break;
                            
                        case "ice-candidate":
                            var iceMessage = JsonUtility.FromJson<WebRTCIceCandidateMessage>(message);
                            if (iceMessage?.data != null)
                            {
                                dataJson = JsonUtility.ToJson(iceMessage.data);
                                LogDebug($"🧊 ICE candidate received");
                            }
                            else
                            {
                                LogError("❌ Не удалось распарсить ICE candidate данные");
                                return;
                            }
                            break;
                            
                        case "session_ready":
                            LogDebug($"🎯 Получен session_ready");
                            var sessionReadyMessage = JsonUtility.FromJson<SessionReadyMessage>(message);
                            if (sessionReadyMessage?.data != null)
                            {
                                HandleSessionReady(sessionReadyMessage);
                            }
                            return; // Не передаем в HandleWebRTCSignal
                            
                        default:
                            LogError($"❌ Неизвестный тип WebRTC сигнала: {basicMessage.signalType}");
                            return;
                    }
                    
                    if (!string.IsNullOrEmpty(dataJson))
                    {
                        LogDebug($"✅ Successfully parsed {basicMessage.signalType} data: {dataJson.Length} chars");
                        HandleWebRTCSignal(basicMessage.signalType, dataJson);
                    }
                    else
                    {
                        LogError($"❌ Пустые данные для сигнала {basicMessage.signalType}");
                    }
                }
                else
                {
                    LogDebug($"⏭️ Проигнорировано сообщение с типом: '{basicMessage.type}'");
                }
            }
            catch (Exception ex)
            {
                LogError($"💥 Ошибка парсинга WebSocket сообщения: {ex.Message}");
                LogError($"💥 Stack trace: {ex.StackTrace}");
                LogError($"💥 Сообщение: {message}");
            }
        }
        
        private void SendWebRTCSignal(string signalType, string data)
        {
            if (_webSocketClient == null) return;
            
            var message = new WebRTCSignalMessage
            {
                type = "webrtc-signal",
                signalType = signalType,
                sessionId = _currentSessionId,
                data = data
            };
            
            var json = JsonUtility.ToJson(message);
            _webSocketClient.SendMessage(json);
            
            LogDebug($"📡 WebRTC сигнал отправлен: {signalType} (session: {_currentSessionId})");
        }
        
        public string GetStatusReport()
        {
            var report = new StringBuilder();
            report.AppendLine($"📊 === WebRTC Video Service Status ===");
            report.AppendLine($"- Initialized: {(_isInitialized ? "✅" : "❌")} {_isInitialized}");
            report.AppendLine($"- Connected: {(_isConnected ? "✅" : "❌")} {_isConnected}");
            report.AppendLine($"- Streaming: {(_isStreaming ? "✅" : "❌")} {_isStreaming}");
            report.AppendLine($"- State: {_connectionState}");
            report.AppendLine($"- Session ID: {_currentSessionId}");
            report.AppendLine($"- PeerConnection: {(_peerConnection != null ? "✅ Active" : "❌ Null")}");
            report.AppendLine();
            
            report.AppendLine($"📊 === Basic Statistics ===");
            report.AppendLine($"- WebRTC Signals: {_webrtcSignalsReceived}");
            report.AppendLine($"- Offers Received: {_offersReceived} 🔥");
            report.AppendLine();
            
            report.AppendLine($"📊 === Video Statistics ===");
            report.AppendLine($"- FPS: {_currentFPS:F1}");
            report.AppendLine($"- Frames: {_receivedFrames}");
            report.AppendLine($"- Bytes: {_bytesReceived:N0}");
            report.AppendLine($"- Connection Time: {ConnectionTime:hh\\:mm\\:ss}");
            report.AppendLine($"- Video Output: {(_videoOutput != null ? _videoOutput.name : "None")}");
            report.AppendLine($"- Remote Track: {(_remoteVideoTrack != null ? "✅ Active" : "❌ None")}");
            
            return report.ToString();
        }
        
        public string GetMessageDiagnostics()
        {
            var diag = new StringBuilder();
            diag.AppendLine($"🔍 === MESSAGE DIAGNOSTICS ===");
            diag.AppendLine($"🎯 WebRTC Signals: {_webrtcSignalsReceived}");
            diag.AppendLine($"🔥 Offers: {_offersReceived}");
            diag.AppendLine();
            
            if (_webrtcSignalsReceived == 0)
            {
                diag.AppendLine("❌ NO WEBRTC SIGNALS RECEIVED!");
                diag.AppendLine("   Check WebSocket connection");
            }
            else if (_offersReceived == 0)
            {
                diag.AppendLine("❌ NO OFFERS RECEIVED!");
                diag.AppendLine("   Check robot offer generation");
            }
            else
            {
                diag.AppendLine("✅ OFFERS ARE BEING RECEIVED!");
            }
            
            return diag.ToString();
        }
        
        public void ResetStatistics()
        {
            _receivedFrames = 0;
            _bytesReceived = 0;
            _currentFPS = 0f;
            _fpsCounter = 0;
            _lastFpsTime = Time.time;
            _webrtcSignalsReceived = 0;
            _offersReceived = 0;
            
            LogDebug("📊 Статистика сброшена");
        }
        

        
        private void LogDebug(string message)
        {
            if (_enableDebugLogs)
            {
                Debug.Log($"[WebRTCVideoService] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[WebRTCVideoService] {message}");
            OnError?.Invoke(message);
        }
        
        private void OnDestroy()
        {
            StopConnection();
            
            if (_webSocketClient != null)
            {
                _webSocketClient.OnMessageReceived -= OnWebSocketMessage;
            }
        }
    }
    
    // Базовая структура для парсинга общих полей WebRTC сообщений
    [Serializable]
    public class BasicWebRTCMessage
    {
        public string type;
        public string signalType;
        // data поле не включаем - будем парсить его отдельно
    }

    // Структура для offer/answer сообщений
    [Serializable] 
    public class WebRTCOfferAnswerMessage
    {
        public string type;
        public string signalType;
        public SdpData data;
    }

    // Универсальная структура с строковым data (для отправки)
    [Serializable]
    public class WebRTCSignalMessage
    {
        public string type;
        public string signalType;
        public string sessionId;
        public string data;
    }
    
    [Serializable]
    public class WebRTCIceCandidateMessage
    {
        public string type;
        public string signalType;
        public IceCandidateData data;
    }
    
    [Serializable]
    public class SdpData
    {
        public string type;
        public string sdp;
        public long timestamp;
    }
    
    [Serializable]
    public class IceCandidateData
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
    }
    
    // Модели данных для ICE конфигурации
    [Serializable]
    public class ICEServerConfiguration
    {
        public ICEServer[] iceServers;
        public int iceCandidatePoolSize;
        public string bundlePolicy;
        public string rtcpMuxPolicy;
    }
    
    [Serializable]
    public class ICEServer
    {
        public string urls;
        public string username;
        public string credential;
    }
    
    [Serializable]
    public class SessionReadyMessage
    {
        public string type;
        public string signalType;
        public string sessionId;
        public SessionReadyData data;
    }
    
    [Serializable]
    public class SessionReadyData
    {
        public ICEServerConfiguration iceConfiguration;
        public SessionInfo sessionInfo;
    }
    
    [Serializable]
    public class SessionInfo
    {
        public bool robotAvailable;
        public bool cameraActive;
    }
} 