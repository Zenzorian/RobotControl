using RobotClient.Control;
using RobotClient.Video;
using SIPSorcery.Net;
using SIPSorcery.SIP.App;
using System.Net;
using System.Threading;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RobotClient.Core
{
    /// <summary>
    /// Поток трансляции видео - управление FFmpeg, WebRTC сигналинг, видео потоки (Linux)
    /// </summary>
    public class VideoStreamingThread : IDisposable
    {
        private readonly WebSocketClient _webSocketClient;
        private RTCPeerConnection? _peerConnection;
        private FFmpegProcessing? _ffmpegProcessing;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly ILogger<VideoStreamingThread> _logger;
        private string? _currentSessionId;
        private bool _isDisposed = false;
        private readonly string[] _ffmpegArgs;
        
        // События для уведомления о состоянии видео
        public event Action<bool>? VideoStreamingStateChanged;
        public event Action<string>? Error;
        
        public bool IsStreaming { get; private set; }
        
        public VideoStreamingThread(WebSocketClient webSocketClient, string[]? ffmpegArgs = null)
        {
            _webSocketClient = webSocketClient ?? throw new ArgumentNullException(nameof(webSocketClient));
            _ffmpegArgs = ffmpegArgs ?? Array.Empty<string>();
            _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<VideoStreamingThread>();
            
            // Подписываемся на сообщения WebSocket для обработки WebRTC сигналинга
            _webSocketClient.MessageReceived += HandleWebSocketMessage;
            
            Console.WriteLine("📹 VideoStreamingThread создан для Linux/FFmpeg");
        }

        /// <summary>
        /// Запуск потока видео трансляции
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                Console.WriteLine("📹 Запуск видео потока (Linux/FFmpeg)...");
                
                // Инициализация FFmpeg
                await InitializeFFmpegAsync();
                
                Console.WriteLine("✅ Видео поток готов к WebRTC соединениям");
                
                // Ожидаем отмены
                await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("🛑 Видео поток остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка в видео потоке: {ex.Message}");
                Error?.Invoke(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Инициализация FFmpeg для захвата видео
        /// </summary>
        private async Task InitializeFFmpegAsync()
        {
            try
            {
                Console.WriteLine("🎬 Инициализация FFmpeg для захвата видео...");
                
                _ffmpegProcessing = new FFmpegProcessing();
                await _ffmpegProcessing.Initialize(_ffmpegArgs);
                
                Console.WriteLine("✅ FFmpeg инициализирован успешно");
                Console.WriteLine($"📺 Видео формат: {_ffmpegProcessing.videoFormat.Name()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка инициализации FFmpeg: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Обработка WebSocket сообщений для WebRTC сигналинга
        /// </summary>
        private async void HandleWebSocketMessage(string message)
        {
            try
            {
                if (message.StartsWith("{"))
                {
                    var jsonDoc = JsonDocument.Parse(message);
                    if (jsonDoc.RootElement.TryGetProperty("type", out var typeElement) && 
                        typeElement.GetString() == "webrtc-signal")
                    {
                        if (jsonDoc.RootElement.TryGetProperty("signalType", out var signalTypeElement))
                        {
                            var signalType = signalTypeElement.GetString();
                            if (signalType == "request_video")
                            {
                                // Для request_video устанавливаем session ID от Unity
                                var sessionId = jsonDoc.RootElement.GetProperty("sessionId").GetString();
                                _currentSessionId = sessionId;
                                Console.WriteLine($"📹 Получен запрос видео от Unity, session ID: {sessionId}");
                                await HandleVideoRequest();
                            }
                            else
                            {
                                await HandleWebRTCSignal(jsonDoc.RootElement);
                            }
                        }
                        else
                        {
                            await HandleWebRTCSignal(jsonDoc.RootElement);
                        }
                    }
                }
                else if (message == "REQUEST_VIDEO")
                {
                    await HandleVideoRequest();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebSocket сообщения: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка запроса видео от контроллера
        /// </summary>
        private async Task HandleVideoRequest()
        {
            try
            {
                Console.WriteLine("📹 Получен запрос видео от контроллера");
                await CreateWebRTCOffer();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка создания WebRTC offer: {ex.Message}");
                Error?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Создание WebRTC offer с FFmpeg RTP потоком
        /// </summary>
        private async Task CreateWebRTCOffer()
        {
            try
            {
                if (_peerConnection != null)
                {
                    Console.WriteLine("⚠️ WebRTC соединение уже существует, закрываем предыдущее");
                    _peerConnection.Close("new connection");
                    _peerConnection = null;
                }

                if (_ffmpegProcessing?.listener == null)
                {
                    throw new InvalidOperationException("FFmpeg RTP listener не инициализирован");
                }

                Console.WriteLine("🎯 Создание WebRTC offer с FFmpeg потоком...");
                
                // Создаем новое WebRTC соединение
                var config = new RTCConfiguration
                {
                    iceServers = new List<RTCIceServer>
                    {
                        new RTCIceServer { urls = "stun:stun.l.google.com:19302" },
                        // TURN сервер будет добавлен через запрос ICE конфигурации
                    }
                };

                _peerConnection = new RTCPeerConnection(config);
                
                // Session ID уже должен быть установлен в HandleWebSocketMessage
                if (string.IsNullOrEmpty(_currentSessionId))
                {
                    _currentSessionId = Guid.NewGuid().ToString();
                    Console.WriteLine($"🆔 Создан новый session ID: {_currentSessionId}");
                }
                else
                {
                    Console.WriteLine($"🆔 Используется session ID от Unity: {_currentSessionId}");
                }

                // Настраиваем обработчики событий
                _peerConnection.onicecandidate += async (candidate) =>
                {
                    if (candidate != null)
                    {
                        Console.WriteLine($"🧊 Отправка ICE кандидата: {candidate.candidate}");
                        await SendICECandidate(candidate);
                    }
                };

                _peerConnection.onconnectionstatechange += (state) =>
                {
                    Console.WriteLine($"🔗 WebRTC состояние соединения: {state}");
                    IsStreaming = state == RTCPeerConnectionState.connected;
                    VideoStreamingStateChanged?.Invoke(IsStreaming);
                    
                    if (state == RTCPeerConnectionState.connected)
                    {
                        // Подключаем FFmpeg RTP поток к WebRTC
                        ConnectFFmpegToWebRTC();
                    }
                };

                _peerConnection.onicegatheringstatechange += (state) =>
                {
                    Console.WriteLine($"🧊 ICE gathering состояние: {state}");
                };

                // Добавляем видео трек с форматом от FFmpeg
              
                var videoTrack = new MediaStreamTrack(SDPMediaTypesEnum.video, false, 
                    new List<SDPAudioVideoMediaFormat> { _ffmpegProcessing.videoFormat }, 
                    MediaStreamStatusEnum.SendOnly);
                    
                _peerConnection.addTrack(videoTrack);
                Console.WriteLine($"📺 Добавлен видео трек: {_ffmpegProcessing.videoFormat.Name()}");
                

                // Создаем offer
                var offer = _peerConnection.createOffer();
                Task setLocalResult = _peerConnection.setLocalDescription(offer);
                
                if (setLocalResult == Task.CompletedTask)
                {
                    Console.WriteLine("✅ Local description установлен");
                }
                else
                {
                    Console.WriteLine($"⚠️ Предупреждение setLocalDescription: {setLocalResult}");
                }

                // Отправляем offer через WebSocket
                var offerMessage = new
                {
                    type = "webrtc-signal",
                    signalType = "offer",
                    sessionId = _currentSessionId,
                    data = new
                    {
                        sdp = offer.sdp,
                        type = offer.type.ToString().ToLower(),
                        sessionId = _currentSessionId
                    }
                };

                await _webSocketClient.SendJsonMessageAsync(offerMessage);
                Console.WriteLine("✅ WebRTC offer отправлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка создания WebRTC offer: {ex.Message}");
                Error?.Invoke(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Подключение FFmpeg RTP потока к WebRTC
        /// </summary>
        private void ConnectFFmpegToWebRTC()
        {
            try
            {
                if (_ffmpegProcessing?.listener == null || _peerConnection == null)
                {
                    Console.WriteLine("❌ FFmpeg listener или PeerConnection не готовы");
                    return;
                }

                Console.WriteLine("🔗 Подключение FFmpeg RTP потока к WebRTC...");

                // Подписываемся на RTP пакеты от FFmpeg и отправляем их через WebRTC
                _ffmpegProcessing.listener.OnRtpPacketReceived += (ep, mediaType, rtpPacket) =>
                {
                    if (mediaType == SDPMediaTypesEnum.video && _peerConnection != null)
                    {
                        try
                        {
                            // Отправляем RTP пакет через WebRTC
                            _peerConnection.SendRtpRaw(mediaType, rtpPacket.Payload, rtpPacket.Header.Timestamp, rtpPacket.Header.MarkerBit, (int)rtpPacket.Header.PayloadType);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Ошибка отправки RTP пакета: {ex.Message}");
                        }
                    }
                };

                Console.WriteLine("✅ FFmpeg поток подключен к WebRTC");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка подключения FFmpeg к WebRTC: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка WebRTC сигналинга
        /// </summary>
        private async Task HandleWebRTCSignal(JsonElement signalElement)
        {
            try
            {
                if (!signalElement.TryGetProperty("signalType", out var signalTypeElement))
                    return;

                var signalType = signalTypeElement.GetString();
                var sessionId = signalElement.GetProperty("sessionId").GetString();
                var data = signalElement.GetProperty("data");

                if (sessionId != _currentSessionId)
                {
                    Console.WriteLine($"⚠️ Получен сигнал для неизвестной сессии: {sessionId}");
                    return;
                }

                Console.WriteLine($"📡 WebRTC сигнал: {signalType}");

                switch (signalType)
                {
                    case "answer":
                        HandleWebRTCAnswer(data);
                        break;
                    case "ice-candidate":
                        HandleWebRTCIceCandidate(data);
                        break;
                    default:
                        Console.WriteLine($"❓ Неизвестный WebRTC сигнал: {signalType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebRTC сигнала: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка WebRTC answer
        /// </summary>
        private void HandleWebRTCAnswer(JsonElement answerData)
        {
            try
            {
                if (_peerConnection == null)
                {
                    Console.WriteLine("❌ PeerConnection не инициализирован для answer");
                    return;
                }

                var sdp = answerData.GetProperty("sdp").GetString();
                var answer = new RTCSessionDescriptionInit
                {
                    type = RTCSdpType.answer,
                    sdp = sdp
                };

                var setRemoteResult = _peerConnection.setRemoteDescription(answer);
                
                if (setRemoteResult == SetDescriptionResultEnum.OK)
                {
                    Console.WriteLine("✅ Remote description установлен");
                    Console.WriteLine("✅ WebRTC answer обработан");
                }
                else
                {
                    Console.WriteLine($"⚠️ Ошибка setRemoteDescription: {setRemoteResult}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebRTC answer: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка ICE кандидата
        /// </summary>
        private void HandleWebRTCIceCandidate(JsonElement candidateData)
        {
            try
            {
                if (_peerConnection == null)
                {
                    Console.WriteLine("❌ PeerConnection не инициализирован для ICE кандидата");
                    return;
                }

                var candidate = candidateData.GetProperty("candidate").GetString();
                var sdpMid = candidateData.GetProperty("sdpMid").GetString();
                var sdpMLineIndex = (ushort)candidateData.GetProperty("sdpMLineIndex").GetInt32();

                var iceCandidateInit = new RTCIceCandidateInit
                {
                    candidate = candidate,
                    sdpMid = sdpMid,
                    sdpMLineIndex = sdpMLineIndex
                };

                try
                {
                    _peerConnection.addIceCandidate(iceCandidateInit);
                    Console.WriteLine($"🧊 ICE кандидат добавлен: {candidate}");
                }
                catch (Exception icEx)
                {
                    Console.WriteLine($"⚠️ Предупреждение при добавлении ICE кандидата: {icEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки ICE кандидата: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка ICE кандидата
        /// </summary>
        private async Task SendICECandidate(RTCIceCandidate candidate)
        {
            try
            {
                var candidateMessage = new
                {
                    type = "webrtc-signal",
                    signalType = "ice-candidate",
                    sessionId = _currentSessionId,
                    data = new
                    {
                        candidate = candidate.candidate,
                        sdpMid = candidate.sdpMid,
                        sdpMLineIndex = (int)candidate.sdpMLineIndex,
                        sessionId = _currentSessionId
                    }
                };

                await _webSocketClient.SendJsonMessageAsync(candidateMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки ICE кандидата: {ex.Message}");
            }
        }

        /// <summary>
        /// Остановка видео потока
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                Console.WriteLine("🛑 Остановка видео потока...");
                
                _cancellationTokenSource?.Cancel();
                
                if (_peerConnection != null)
                {
                    _peerConnection.Close("stopping");
                    _peerConnection = null;
                }
                
                if (_ffmpegProcessing != null)
                {
                    _ffmpegProcessing.exitCts?.Cancel();
                    _ffmpegProcessing = null;
                }
                
                IsStreaming = false;
                VideoStreamingStateChanged?.Invoke(false);
                
                Console.WriteLine("✅ Видео поток остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка остановки видео потока: {ex.Message}");
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _webSocketClient.MessageReceived -= HandleWebSocketMessage;
                StopAsync().Wait();
                _cancellationTokenSource?.Dispose();
                _isDisposed = true;
            }
        }
    }
}
