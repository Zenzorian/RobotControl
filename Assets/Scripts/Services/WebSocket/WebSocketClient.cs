using WebSocketSharp;
using UnityEngine;
using System;
using System.Collections.Concurrent;

namespace Scripts.Services
{
    public class WebSocketClient: IWebSocketClient, IDisposable
    {
        public WebSocket GetWebSocket{get{return _webSocket;} private set{}}
        public bool IsConnected{get; private set;}
        
        public event Action<string> OnMessageReceived;

        public const string SERVER_ADDRESS = "193.169.240.11";
        public const int SERVER_PORT = 8080;

        private readonly IStatus _status;
        private WebSocket _webSocket;
        private bool _disposed = false;
        private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        public WebSocketClient(IStatus status)
        {
            _status = status;

            _webSocket = new WebSocket($"ws://{SERVER_ADDRESS}:{SERVER_PORT}");
                
            _webSocket.OnOpen += OnWebSocketOpen;
            _webSocket.OnMessage += OnWebSocketMessage;
            _webSocket.OnError += OnWebSocketError;
            _webSocket.OnClose += OnWebSocketClose;           
               
            _webSocket.Connect();
        }        

        private void OnWebSocketOpen(object sender, System.EventArgs e)
        {           
            _mainThreadActions.Enqueue(() => {
                _status.UpdateServerStatus(true);
                _status.Info("Подключено к серверу");

                _webSocket.Send("REGISTER!CONTROLLER");
                IsConnected = true;
            });
        }
        
        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            string message = e.Data;
            _mainThreadActions.Enqueue(() => {
                try
                {
                    //Debug.Log($"Получено сообщение: {message}");
                    _status.UpdateServerStatus(true);

                    if (message == "REGISTERED!CONTROLLER") 
                    {               
                        _status.Info("Контроллер успешно зарегистрирован");
                    }
                    else if (message.StartsWith("ERROR!"))
                    {
                        _status.Error($"Ошибка сервера: {message.Substring(6)}");
                    }
                    
                    // Уведомляем подписчиков о получении сообщения
                    try
                    {
                        OnMessageReceived?.Invoke(message);
                    }
                    catch (Exception eventEx)
                    {
                        Debug.LogError($"Ошибка в обработчике события OnMessageReceived: {eventEx.Message}");
                        Debug.LogError($"Stack trace: {eventEx.StackTrace}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Ошибка при обработке сообщения: {ex.Message}");
                }
            });            
        }
        
        private void OnWebSocketError(object sender, ErrorEventArgs e)
        {
            string errorMessage = e.Message;
            _mainThreadActions.Enqueue(() => {
                _status.Error($"Ошибка WebSocket: {errorMessage}");            
                IsConnected = false;
                _status.UpdateServerStatus(false);
            });
        }
        
        private void OnWebSocketClose(object sender, CloseEventArgs e)
        {           
            ushort code = e.Code;
            _mainThreadActions.Enqueue(() => {
                _status.Info($"Отключено от сервера: {code}");
                IsConnected = false;
                _status.UpdateServerStatus(false);
            });
        }

        public void SendMessage(string message)
        {
            if (_webSocket != null && IsConnected && _webSocket.ReadyState == WebSocketState.Open)
            {
                try
                {
                    _webSocket.Send(message);
                    Debug.Log($"Отправлено сообщение: {message}");
                }
                catch (Exception ex)
                {
                    _mainThreadActions.Enqueue(() => {
                        _status.Error($"Ошибка отправки сообщения: {ex.Message}");
                    });
                    Debug.LogError($"Ошибка отправки WebSocket сообщения: {ex.Message}");
                }
            }
            else
            {
                _mainThreadActions.Enqueue(() => {
                    _status.Warning("Невозможно отправить сообщение - WebSocket не подключен");
                });
                Debug.LogWarning($"Попытка отправить сообщение при неактивном соединении: {message}");
            }
        }

        /// <summary>
        /// Отправка JSON сообщения (для WebRTC сигналинга)
        /// </summary>
        public void SendJsonMessage<T>(T messageObject)
        {
            try
            {
                string json = JsonUtility.ToJson(messageObject);
                Debug.Log($"📤 Отправляемый JSON: {json}");
                SendMessage(json);
            }
            catch (Exception ex)
            {
                _mainThreadActions.Enqueue(() => {
                    _status.Error($"Ошибка отправки JSON: {ex.Message}");
                });
                Debug.LogError($"Ошибка отправки JSON сообщения: {ex.Message}");
            }
        }
        
        public void Update()
        {
            while (_mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Ошибка в обработчике WebSocket: {ex.Message}");
                }
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {                    
                    if (IsConnected && _webSocket != null && _webSocket.ReadyState == WebSocketState.Open)
                    {
                        try
                        {
                            _webSocket.Send("DISCONNECT!CONTROLLER");
                            _mainThreadActions.Enqueue(() => {
                                _status.Info("Отправлен запрос на отключение");
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Ошибка при отправке сообщения отключения: {ex.Message}");
                        }
                    }                    
                   
                    if (_webSocket != null)
                    {
                        _webSocket.OnOpen -= OnWebSocketOpen;
                        _webSocket.OnMessage -= OnWebSocketMessage;
                        _webSocket.OnError -= OnWebSocketError;
                        _webSocket.OnClose -= OnWebSocketClose;
                        
                        if (_webSocket.ReadyState == WebSocketState.Open || 
                            _webSocket.ReadyState == WebSocketState.Connecting)
                        {
                            _webSocket.Close();
                        }
                    }
                }               
                
                _webSocket = null;
                IsConnected = false;
                
                _disposed = true;
            }
        }
        
        ~WebSocketClient()
        {
            Dispose(false);
        }
    }  
}