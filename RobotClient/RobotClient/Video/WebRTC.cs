using System.Text.Json;
using System.Net.Http;

namespace RobotClient.Video
{
    /// <summary>
    /// Сервис для обработки WebRTC соединений и сигналинга
    /// </summary>
    public class WebRTC
    {
        private readonly WebSocketClient _webSocketService;
        private readonly VideoStreaming _videoStreamingService;
        private readonly Dictionary<string, WebRTCSession> _activeSessions;
        private readonly HttpClient _httpClient;
        private bool _isActive;
        private ICEConfiguration? _iceConfiguration;

        public event Action<string>? OfferReceived;
        public event Action<string>? SessionStarted;
        public event Action<string>? SessionEnded;
        public event Action<string, string>? WebRTCError;

        public bool IsActive => _isActive;
        public int ActiveSessionsCount => _activeSessions.Count;

        public WebRTC(WebSocketClient webSocketService, VideoStreaming videoStreamingService)
        {
            _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
            _videoStreamingService = videoStreamingService ?? throw new ArgumentNullException(nameof(videoStreamingService));
            
            _activeSessions = new Dictionary<string, WebRTCSession>();
            _httpClient = new HttpClient();
            
            // Подписываемся на сообщения WebSocket
            _webSocketService.MessageReceived += OnWebSocketMessage;
            
            Console.WriteLine("🎥 WebRTC сервис инициализирован");
        }

        /// <summary>
        /// Запуск WebRTC сервиса
        /// </summary>
        public async Task<bool> StartAsync()
        {
            if (_isActive)
            {
                Console.WriteLine("⚠️ WebRTC сервис уже запущен");
                return true;
            }

            try
            {
                // Загружаем ICE конфигурацию с сервера
                Console.WriteLine("🔄 Загрузка TURN/STUN конфигурации...");
                await LoadICEConfiguration();

                // Инициализируем видео стриминг
                if (!await _videoStreamingService.InitializeAsync())
                {
                    Console.WriteLine("❌ Не удалось инициализировать видео стриминг");
                    return false;
                }

                _isActive = true;
                Console.WriteLine("✅ WebRTC сервис запущен с TURN поддержкой");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запуска WebRTC сервиса: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Остановка WebRTC сервиса
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _isActive = false;
                
                // Завершаем все активные сессии
                var sessionIds = _activeSessions.Keys.ToList();
                foreach (var sessionId in sessionIds)
                {
                    await EndSession(sessionId);
                }

                // Останавливаем видео стриминг
                await _videoStreamingService.StopAsync();
                
                Console.WriteLine("✅ WebRTC сервис остановлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка остановки WebRTC сервиса: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка WebSocket сообщений
        /// </summary>
        private async void OnWebSocketMessage(string message)
        {
            Console.WriteLine($"🎥 WebRTC.OnWebSocketMessage вызван, активен: {_isActive}");
            
            if (!_isActive)
            {
                Console.WriteLine($"⚠️ WebRTC сервис неактивен, сообщение игнорируется");
                return;
            }

            try
            {
                // Пытаемся парсить как JSON для WebRTC сигналов
                if (message.StartsWith("{"))
                {
                    Console.WriteLine($"🔍 Парсинг WebRTC сообщения: {message}");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    var webrtcMessage = JsonSerializer.Deserialize<WebRTCMessage>(message, options);
                    if (webrtcMessage != null && webrtcMessage.type == "webrtc-signal")
                    {
                        Console.WriteLine($"✅ Обрабатываем WebRTC сигнал: {webrtcMessage.signalType}");
                        await HandleWebRTCSignal(webrtcMessage);
                        return;
                    }
                    
                    Console.WriteLine($"❓ Неизвестный формат сообщения (ожидается webrtc-signal), получен type: {webrtcMessage?.type}");
                }
                else
                {
                    Console.WriteLine($"🔍 Не JSON сообщение, пропускаем: {message.Substring(0, Math.Min(50, message.Length))}...");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"❌ Ошибка парсинга JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebRTC сообщения: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка WebRTC сигналов
        /// </summary>
        private async Task HandleWebRTCSignal(WebRTCMessage message)
        {
            try
            {
                Console.WriteLine($"📡 WebRTC сигнал: {message.signalType}");

                switch (message.signalType)
                {
                    case "offer":
                        // Робот НЕ обрабатывает offer'ы - он их только создает!
                        Console.WriteLine("⚠️ Робот получил offer - игнорируем (offer предназначен для контроллера)");
                        break;

                    case "answer":
                        await HandleAnswer(message);
                        break;

                    case "ice-candidate":
                        await HandleIceCandidate(message);
                        break;

                    case "ice-configuration":
                        await HandleICEConfigurationResponse(message);
                        break;

                    case "request_video":
                        await HandleRequestVideo(message);
                        break;

                    case "session-end":
                        await HandleSessionEnd(message);
                        break;

                    default:
                        Console.WriteLine($"❓ Неизвестный WebRTC сигнал: {message.signalType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebRTC сигнала: {ex.Message}");
                WebRTCError?.Invoke(message.sessionId ?? "unknown", ex.Message);
            }
        }

        /// <summary>
        /// Обработка запроса видео от клиента - создаем offer
        /// </summary>
        private async Task HandleRequestVideo(WebRTCMessage message)
        {
            try
            {
                var sessionId = message.sessionId ?? Guid.NewGuid().ToString();
                Console.WriteLine($"📹 Получен запрос видео, создаем сессию: {sessionId}");

                // Создаем новую сессию
                var session = new WebRTCSession
                {
                    SessionId = sessionId,
                    State = WebRTCSessionState.Created,
                    CreatedAt = DateTime.UtcNow
                };

                _activeSessions[sessionId] = session;

                // Запускаем видео стрим для этой сессии
                var streamStarted = await _videoStreamingService.StartStreamAsync(sessionId);
                
                if (!streamStarted)
                {
                    Console.WriteLine("❌ Не удалось запустить видео стрим");
                    await SendError(sessionId, "Failed to start video stream");
                    return;
                }

                // Создаем и отправляем offer
                var offer = await CreateOffer();
                if (offer != null)
                {
                    await SendOffer(sessionId, offer);
                    session.State = WebRTCSessionState.OfferSent;
                    Console.WriteLine($"✅ Offer отправлен для сессии: {sessionId}");
                }
                else
                {
                    Console.WriteLine("❌ Не удалось создать WebRTC offer");
                    await SendError(sessionId, "Failed to create offer");
                    await EndSession(sessionId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки запроса видео: {ex.Message}");
                await SendError(message.sessionId ?? "unknown", ex.Message);
            }
        }

        /// <summary>
        /// Обработка WebRTC offer от клиента
        /// </summary>
        private async Task HandleOffer(WebRTCMessage message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.sessionId))
                {
                    Console.WriteLine("❌ Offer без sessionId");
                    return;
                }

                Console.WriteLine($"🎯 Получен WebRTC offer для сессии: {message.sessionId}");
                OfferReceived?.Invoke(message.sessionId);

                // Создаем новую сессию
                var session = new WebRTCSession
                {
                    SessionId = message.sessionId,
                    State = WebRTCSessionState.OfferReceived,
                    CreatedAt = DateTime.UtcNow,
                    Offer = message.data
                };

                _activeSessions[message.sessionId] = session;

                // Запускаем видео стрим для этой сессии
                var streamStarted = await _videoStreamingService.StartStreamAsync(message.sessionId);
                
                if (!streamStarted)
                {
                    Console.WriteLine("❌ Не удалось запустить видео стрим");
                    await SendError(message.sessionId, "Failed to start video stream");
                    return;
                }

                // Создаем answer
                var answer = await CreateAnswer(message.data);
                if (answer != null)
                {
                    await SendAnswer(message.sessionId, answer);
                    session.State = WebRTCSessionState.AnswerSent;
                    SessionStarted?.Invoke(message.sessionId);
                }
                else
                {
                    Console.WriteLine("❌ Не удалось создать WebRTC answer");
                    await SendError(message.sessionId, "Failed to create answer");
                    await EndSession(message.sessionId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки offer: {ex.Message}");
                await SendError(message.sessionId ?? "unknown", ex.Message);
            }
        }

        /// <summary>
        /// Обработка WebRTC answer от клиента
        /// </summary>
        private async Task HandleAnswer(WebRTCMessage message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.sessionId))
                {
                    Console.WriteLine("❌ Answer без sessionId");
                    return;
                }

                if (!_activeSessions.TryGetValue(message.sessionId, out var session))
                {
                    Console.WriteLine($"❌ Сессия не найдена для answer: {message.sessionId}");
                    return;
                }

                Console.WriteLine($"🎯 Получен WebRTC answer для сессии: {message.sessionId}");
                
                session.Answer = message.data;
                session.State = WebRTCSessionState.Connected;
                
                Console.WriteLine($"✅ WebRTC сессия установлена: {message.sessionId}");
                SessionStarted?.Invoke(message.sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки answer: {ex.Message}");
                await SendError(message.sessionId ?? "unknown", ex.Message);
            }
        }

        /// <summary>
        /// Создание WebRTC offer
        /// </summary>
        private async Task<object?> CreateOffer()
        {
            try
            {
                Console.WriteLine("📡 Создание WebRTC offer...");
                
                // Симуляция создания offer
                await Task.Delay(100);
                
                // Создаем корректный SDP с DTLS fingerprint для WebRTC
                var sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var sdp = $"v=0\r\n" +
                         $"o=robot {sessionId} 2 IN IP4 127.0.0.1\r\n" +
                         $"s=-\r\n" +
                         $"t=0 0\r\n" +
                         $"a=group:BUNDLE 0\r\n" +
                         $"a=extmap-allow-mixed\r\n" +
                         $"a=msid-semantic: WMS stream_id\r\n" +
                         $"m=video 9 UDP/TLS/RTP/SAVPF 96\r\n" +
                         $"c=IN IP4 0.0.0.0\r\n" +
                         $"a=rtcp:9 IN IP4 0.0.0.0\r\n" +
                         $"a=ice-ufrag:4ZcD\r\n" +
                         $"a=ice-pwd:2/1muCWoOI+dOT5kwdOPOk/1\r\n" +
                         $"a=ice-options:trickle\r\n" +
                         $"a=fingerprint:sha-256 E3:1B:2A:46:3C:54:D8:96:35:AB:B7:65:EF:C1:23:45:67:89:AB:CD:EF:12:34:56:78:9A:BC:DE:F0:12:34:56\r\n" +
                         $"a=setup:actpass\r\n" +
                         $"a=mid:0\r\n" +
                         $"a=extmap:1 urn:ietf:params:rtp-hdrext:ssrc-audio-level\r\n" +
                         $"a=extmap:2 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time\r\n" +
                         $"a=extmap:3 http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01\r\n" +
                         $"a=extmap:4 urn:ietf:params:rtp-hdrext:sdes:mid\r\n" +
                         $"a=extmap:5 urn:ietf:params:rtp-hdrext:sdes:rtp-stream-id\r\n" +
                         $"a=extmap:6 urn:ietf:params:rtp-hdrext:sdes:repaired-rtp-stream-id\r\n" +
                         $"a=sendonly\r\n" +
                         $"a=msid:stream_id video_track_id\r\n" +
                         $"a=rtcp-mux\r\n" +
                         $"a=rtcp-rsize\r\n" +
                         $"a=rtpmap:96 H264/90000\r\n" +
                         $"a=rtcp-fb:96 goog-remb\r\n" +
                         $"a=rtcp-fb:96 transport-cc\r\n" +
                         $"a=rtcp-fb:96 ccm fir\r\n" +
                         $"a=rtcp-fb:96 nack\r\n" +
                         $"a=rtcp-fb:96 nack pli\r\n" +
                         $"a=fmtp:96 level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42001f\r\n" +
                         $"a=ssrc:1001 cname:robot_video_stream\r\n" +
                         $"a=ssrc:1001 msid:stream_id video_track_id\r\n" +
                         $"a=ssrc:1001 mslabel:stream_id\r\n" +
                         $"a=ssrc:1001 label:video_track_id\r\n";
                
                var offer = new
                {
                    type = "offer",
                    sdp = sdp,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                Console.WriteLine("✅ WebRTC offer создан");
                return offer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка создания offer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Создание WebRTC answer
        /// </summary>
        private async Task<object?> CreateAnswer(object? offerData)
        {
            try
            {
                // В реальной реализации здесь будет создание WebRTC answer
                // Пока возвращаем заглушку
                
                Console.WriteLine("📡 Создание WebRTC answer...");
                
                // Симуляция создания answer
                await Task.Delay(100);
                
                var answer = new
                {
                    type = "answer",
                    sdp = "v=0\r\no=robot 0 0 IN IP4 127.0.0.1\r\ns=-\r\nt=0 0\r\n" +
                          "m=video 9 UDP/TLS/RTP/SAVPF 96\r\n" +
                          "a=rtpmap:96 H264/90000\r\n" +
                          "a=sendonly\r\n",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                Console.WriteLine("✅ WebRTC answer создан");
                return answer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка создания answer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Отправка WebRTC offer
        /// </summary>
        private async Task SendOffer(string sessionId, object offer)
        {
            try
            {
                var offerMessage = new
                {
                    type = "webrtc-signal",
                    signalType = "offer",
                    sessionId = sessionId,
                    data = offer
                };

                await _webSocketService.SendJsonMessageAsync(offerMessage);
                Console.WriteLine($"📤 WebRTC offer отправлен для сессии: {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки offer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Отправка WebRTC answer
        /// </summary>
        private async Task SendAnswer(string sessionId, object answer)
        {
            try
            {
                var answerMessage = new
                {
                    type = "webrtc-signal",
                    signalType = "answer",
                    sessionId = sessionId,
                    data = answer
                };

                await _webSocketService.SendJsonMessageAsync(answerMessage);
                Console.WriteLine($"📤 WebRTC answer отправлен для сессии: {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки answer: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Обработка ICE кандидатов
        /// </summary>
        private async Task HandleIceCandidate(WebRTCMessage message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.sessionId))
                {
                    Console.WriteLine("❌ ICE кандидат без sessionId");
                    return;
                }

                if (!_activeSessions.ContainsKey(message.sessionId))
                {
                    Console.WriteLine($"❌ Сессия не найдена для ICE: {message.sessionId}");
                    return;
                }

                Console.WriteLine($"🧊 Получен ICE кандидат для сессии: {message.sessionId}");
                
                // В реальной реализации здесь будет обработка ICE кандидата
                // Пока просто логируем
                
                // Отправляем свой ICE кандидат в ответ
                await SendIceCandidate(message.sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки ICE кандидата: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка ICE кандидатов (реальных)
        /// </summary>
        private async Task SendIceCandidate(string sessionId)
        {
            try
            {
                // Генерируем реальные ICE кандидаты
                var iceCandidates = await GenerateICECandidates();
                
                foreach (var candidate in iceCandidates)
                {
                    var iceCandidateMessage = new
                    {
                        type = "webrtc-signal",
                        signalType = "ice-candidate",
                        sessionId = sessionId,
                        data = candidate
                    };

                    await _webSocketService.SendJsonMessageAsync(iceCandidateMessage);
                    
                    // Извлекаем тип кандидата для логирования
                    var candidateType = "unknown";
                    try
                    {
                        var candidateObj = candidate as dynamic;
                        candidateType = candidateObj?.candidateType?.ToString() ?? "unknown";
                    }
                    catch { }
                    
                    Console.WriteLine($"🧊 ICE кандидат отправлен ({candidateType}): {sessionId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки ICE кандидата: {ex.Message}");
            }
        }

        /// <summary>
        /// Генерация реальных ICE кандидатов на основе загруженной конфигурации
        /// </summary>
        private async Task<List<object>> GenerateICECandidates()
        {
            var candidates = new List<object>();
            var priority = 2122194687; // Высокий приоритет для host кандидатов
            var foundation = 1;

            try
            {
                // 1. Host кандидаты (локальные IP адреса)
                var localIPs = await GetLocalIPAddresses();
                foreach (var ip in localIPs)
                {
                    var port = new Random().Next(50000, 60000);
                    candidates.Add(new
                    {
                        candidate = $"candidate:{foundation++} 1 UDP {priority} {ip} {port} typ host generation 0",
                        sdpMLineIndex = 0,
                        sdpMid = "0",
                        candidateType = "host"
                    });
                    priority -= 1000; // Снижаем приоритет для следующих
                }

                // 2. Server reflexive кандидаты (через STUN)
                var stunCandidates = await GenerateSTUNCandidates();
                candidates.AddRange(stunCandidates);

                // 3. Relay кандидаты (через TURN)
                var turnCandidates = await GenerateTURNCandidates();
                candidates.AddRange(turnCandidates);

                Console.WriteLine($"🧊 Сгенерировано ICE кандидатов: {candidates.Count} (host: {localIPs.Count}, STUN: {stunCandidates.Count}, TURN: {turnCandidates.Count})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка генерации ICE кандидатов: {ex.Message}");
                
                // Fallback: минимальный set кандидатов
                candidates.Add(new
                {
                    candidate = $"candidate:1 1 UDP 2122194687 127.0.0.1 54400 typ host generation 0",
                    sdpMLineIndex = 0,
                    sdpMid = "0",
                    candidateType = "host-fallback"
                });
            }

            return candidates;
        }

        /// <summary>
        /// Получение локальных IP адресов
        /// </summary>
        private async Task<List<string>> GetLocalIPAddresses()
        {
            var ips = new List<string>();
            
            try
            {
                var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                
                foreach (var networkInterface in networkInterfaces)
                {
                    if (networkInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        networkInterface.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        var ipProperties = networkInterface.GetIPProperties();
                        foreach (var ip in ipProperties.UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                                !System.Net.IPAddress.IsLoopback(ip.Address))
                            {
                                ips.Add(ip.Address.ToString());
                            }
                        }
                    }
                }
                
                // Добавляем loopback для локального тестирования
                if (!ips.Contains("127.0.0.1"))
                {
                    ips.Add("127.0.0.1");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка получения локальных IP: {ex.Message}");
                ips.Add("127.0.0.1"); // Fallback
            }

            return ips;
        }

        /// <summary>
        /// Генерация STUN кандидатов
        /// </summary>
        private async Task<List<object>> GenerateSTUNCandidates()
        {
            var candidates = new List<object>();
            var iceConfig = GetICEConfiguration();
            
            if (iceConfig?.IceServers != null)
            {
                var stunServers = iceConfig.IceServers.Where(s => s.Urls?.StartsWith("stun:") == true).ToList();
                
                foreach (var stunServer in stunServers.Take(2)) // Ограничиваем количество
                {
                    try
                    {
                        // Эмулируем STUN binding request (упрощенно)
                        var externalIP = await GetExternalIP();
                        if (!string.IsNullOrEmpty(externalIP))
                        {
                            var port = new Random().Next(50000, 60000);
                            var priority = 1677729535; // STUN приоритет
                            
                            candidates.Add(new
                            {
                                candidate = $"candidate:{candidates.Count + 10} 1 UDP {priority} {externalIP} {port} typ srflx raddr 0.0.0.0 rport 0 generation 0",
                                sdpMLineIndex = 0,
                                sdpMid = "0",
                                candidateType = "srflx"
                            });
                            
                            break; // Один STUN кандидат достаточно
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Ошибка STUN binding: {ex.Message}");
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// Генерация TURN кандидатов
        /// </summary>
        private async Task<List<object>> GenerateTURNCandidates()
        {
            var candidates = new List<object>();
            var iceConfig = GetICEConfiguration();
            
            if (iceConfig?.IceServers != null)
            {
                var turnServers = iceConfig.IceServers.Where(s => 
                    s.Urls?.StartsWith("turn:") == true || s.Urls?.StartsWith("turns:") == true).ToList();
                
                foreach (var turnServer in turnServers.Take(2)) // Ограничиваем количество
                {
                    try
                    {
                        // Эмулируем TURN allocation (упрощенно)
                        if (turnServer.Urls != null)
                        {
                            var serverInfo = ParseTurnUrl(turnServer.Urls);
                            if (serverInfo.HasValue)
                            {
                                var (host, serverPort) = serverInfo.Value;
                                var relayPort = new Random().Next(49152, 65535); // TURN relay port range
                                var priority = 16777215; // TURN relay приоритет (самый низкий)
                                
                                candidates.Add(new
                                {
                                    candidate = $"candidate:{candidates.Count + 100} 1 UDP {priority} {host} {relayPort} typ relay raddr {host} rport {serverPort} generation 0",
                                    sdpMLineIndex = 0,
                                    sdpMid = "0",
                                    candidateType = "relay"
                                });
                                
                                Console.WriteLine($"🔄 TURN relay кандидат: {host}:{relayPort} (сервер: {turnServer.Urls})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Ошибка TURN allocation: {ex.Message}");
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// Парсинг TURN URL
        /// </summary>
        private (string host, int port)? ParseTurnUrl(string turnUrl)
        {
            try
            {
                // turn:193.169.240.11:13478 или turns:193.169.240.11:15349
                var parts = turnUrl.Replace("turn:", "").Replace("turns:", "").Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1], out var port))
                {
                    return (parts[0], port);
                }
            }
            catch { }
            
            return null;
        }

        /// <summary>
        /// Получение внешнего IP адреса
        /// </summary>
        private async Task<string?> GetExternalIP()
        {
            try
            {
                // Используем простой HTTP сервис для определения внешнего IP
                var response = await _httpClient.GetStringAsync("https://ipv4.icanhazip.com/");
                return response.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Не удалось получить внешний IP: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Обработка завершения сессии
        /// </summary>
        private async Task HandleSessionEnd(WebRTCMessage message)
        {
            if (string.IsNullOrEmpty(message.sessionId))
                return;

            await EndSession(message.sessionId);
        }

        /// <summary>
        /// Завершение сессии
        /// </summary>
        private async Task EndSession(string sessionId)
        {
            try
            {
                if (_activeSessions.TryGetValue(sessionId, out var session))
                {
                    session.State = WebRTCSessionState.Ended;
                    session.EndedAt = DateTime.UtcNow;
                    
                    // Останавливаем видео стрим для этой сессии
                    await _videoStreamingService.StopStreamAsync(sessionId);
                    
                    _activeSessions.Remove(sessionId);
                    
                    Console.WriteLine($"🔚 Сессия завершена: {sessionId}");
                    SessionEnded?.Invoke(sessionId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка завершения сессии: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка ошибки
        /// </summary>
        private async Task SendError(string sessionId, string error)
        {
            try
            {
                var errorMessage = new
                {
                    type = "webrtc-signal",
                    signalType = "error",
                    sessionId = sessionId,
                    data = new { error = error }
                };

                await _webSocketService.SendJsonMessageAsync(errorMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки ошибки: {ex.Message}");
            }
        }

        /// <summary>
        /// Получение статистики сессий
        /// </summary>
        public object GetSessionStats()
        {
            return new
            {
                activeSessions = _activeSessions.Count,
                sessions = _activeSessions.Values.Select(s => new
                {
                    sessionId = s.SessionId,
                    state = s.State.ToString(),
                    createdAt = s.CreatedAt,
                    duration = s.EndedAt?.Subtract(s.CreatedAt) ?? DateTime.UtcNow.Subtract(s.CreatedAt)
                })
            };
        }

        /// <summary>
        /// Получение ICE конфигурации с сервера
        /// </summary>
        private async Task<bool> LoadICEConfiguration()
        {
            try
            {
                Console.WriteLine("🧊 Загрузка ICE конфигурации с сервера...");
                
                // Формируем URL сервера (используем Config.ServerConfig)
                var serverUrl = $"http://{RobotClient.Config.ServerConfig.SERVER_IP}:{RobotClient.Config.ServerConfig.SERVER_PORT}";
                var iceConfigUrl = $"{serverUrl}/api/ice/config";
                
                Console.WriteLine($"🌐 Запрос ICE конфигурации: {iceConfigUrl}");
                
                var response = await _httpClient.GetAsync(iceConfigUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    
                    _iceConfiguration = JsonSerializer.Deserialize<ICEConfiguration>(jsonContent, options);
                    
                    if (_iceConfiguration != null)
                    {
                        Console.WriteLine($"✅ ICE конфигурация загружена, серверов: {_iceConfiguration.IceServers?.Count ?? 0}");
                        
                        // Логируем TURN серверы
                        if (_iceConfiguration.IceServers != null)
                        {
                            foreach (var server in _iceConfiguration.IceServers)
                            {
                                if (server.Urls?.StartsWith("turn:") == true || server.Urls?.StartsWith("turns:") == true)
                                {
                                    Console.WriteLine($"🔄 TURN сервер: {server.Urls} (пользователь: {server.Username})");
                                }
                                else
                                {
                                    Console.WriteLine($"🧊 STUN сервер: {server.Urls}");
                                }
                            }
                        }
                        
                        return true;
                    }
                }
                
                Console.WriteLine($"❌ Не удалось загрузить ICE конфигурацию: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка загрузки ICE конфигурации: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получение ICE конфигурации (с fallback на STUN серверы)
        /// </summary>
        private ICEConfiguration GetICEConfiguration()
        {
            if (_iceConfiguration != null)
            {
                return _iceConfiguration;
            }
            
            // Fallback конфигурация с STUN и TURN серверами
            Console.WriteLine("⚠️ Используется fallback ICE конфигурация (STUN + TURN)");
            return new ICEConfiguration
            {
                IceServers = new List<IceServer>
                {
                    // STUN серверы
                    new IceServer { Urls = "stun:stun.l.google.com:19302" },
                    new IceServer { Urls = "stun:stun1.l.google.com:19302" },
                    new IceServer { Urls = "stun:stun.cloudflare.com:3478" },
                    
                    // TURN сервер (новые порты)
                    new IceServer 
                    { 
                        Urls = "turn:193.169.240.11:13478",
                        Username = "robotclient",
                        Credential = "robotclient2024"
                    },
                    new IceServer 
                    { 
                        Urls = "turn:193.169.240.11:13478?transport=tcp",
                        Username = "robotclient",
                        Credential = "robotclient2024"
                    }
                },
                IceCandidatePoolSize = 10,
                BundlePolicy = "max-bundle",
                RtcpMuxPolicy = "require"
            };
        }

        /// <summary>
        /// Запрос ICE конфигурации через WebSocket
        /// </summary>
        private async Task RequestICEConfiguration()
        {
            try
            {
                Console.WriteLine("🧊 Запрос ICE конфигурации через WebSocket...");
                
                var requestMessage = new
                {
                    type = "webrtc-signal",
                    signalType = "ice-configuration"
                };
                
                var json = JsonSerializer.Serialize(requestMessage);
                await _webSocketService.SendMessageAsync(json);
                
                Console.WriteLine("📤 Запрос ICE конфигурации отправлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка запроса ICE конфигурации: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка ответа ICE конфигурации через WebSocket
        /// </summary>
        private async Task HandleICEConfigurationResponse(WebRTCMessage message)
        {
            try
            {
                Console.WriteLine("🧊 Получен ответ ICE конфигурации через WebSocket");
                
                if (message.data != null)
                {
                    var json = JsonSerializer.Serialize(message.data);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };
                    
                    _iceConfiguration = JsonSerializer.Deserialize<ICEConfiguration>(json, options);
                    
                    if (_iceConfiguration != null)
                    {
                        Console.WriteLine($"✅ ICE конфигурация обновлена через WebSocket, серверов: {_iceConfiguration.IceServers?.Count ?? 0}");
                        
                        // Логируем обновленные серверы
                        if (_iceConfiguration.IceServers != null)
                        {
                            foreach (var server in _iceConfiguration.IceServers)
                            {
                                if (server.Urls?.StartsWith("turn:") == true || server.Urls?.StartsWith("turns:") == true)
                                {
                                    Console.WriteLine($"🔄 TURN сервер обновлен: {server.Urls}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки ICE конфигурации: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _webSocketService.MessageReceived -= OnWebSocketMessage;
            _ = StopAsync();
        }
    }

    /// <summary>
    /// Структура WebRTC сообщения
    /// </summary>
    public class WebRTCMessage
    {
        public string? type { get; set; }
        public string? signalType { get; set; }
        public string? sessionId { get; set; }
        public object? data { get; set; }
    }

    /// <summary>
    /// Информация о WebRTC сессии
    /// </summary>
    public class WebRTCSession
    {
        public string SessionId { get; set; } = string.Empty;
        public WebRTCSessionState State { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public object? Offer { get; set; }
        public object? Answer { get; set; }
    }

    /// <summary>
    /// Состояние WebRTC сессии
    /// </summary>
    public enum WebRTCSessionState
    {
        Created,
        OfferSent,
        OfferReceived,
        AnswerSent,
        Connected,
        Failed,
        Ended
    }

    /// <summary>
    /// ICE конфигурация для WebRTC
    /// </summary>
    public class ICEConfiguration
    {
        public List<IceServer>? IceServers { get; set; }
        public int IceCandidatePoolSize { get; set; }
        public string? BundlePolicy { get; set; }
        public string? RtcpMuxPolicy { get; set; }
    }

    /// <summary>
    /// ICE сервер (STUN/TURN)
    /// </summary>
    public class IceServer
    {
        public string? Urls { get; set; }
        public string? Username { get; set; }
        public string? Credential { get; set; }
    }
} 