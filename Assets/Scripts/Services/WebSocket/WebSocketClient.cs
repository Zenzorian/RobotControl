using WebSocketSharp;
using UnityEngine;
using Scripts.UI;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Scripts.Services
{
    public class WebSocketClient: IWebSocketClient, IDisposable
    {
        public WebSocket GetWebSocket{get{return _webSocket;} private set{}}
        public bool IsConnected{get; private set;}
       
        private readonly IStatus _status;
        private WebSocket _webSocket;
        private bool _disposed = false;
        private ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        public WebSocketClient(string serverAddress, int serverPort, IStatus status)
        {
            _status = status;

            _webSocket = new WebSocket($"ws://{serverAddress}:{serverPort}");
                
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
                _status.Info("Подключено");

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
                    Debug.Log($"Получено сообщение: {message}");
                    _status.UpdateServerStatus(true);

                    if (message == "REGISTERED!CONTROLLER") 
                    {               
                        _status.Info("Контроллер успешно зарегистрирован");
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
                _status.Info($"Отключено: {code}");
                IsConnected = false;
                _status.UpdateServerStatus(false);
            });
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