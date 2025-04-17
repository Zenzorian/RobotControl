using WebSocketSharp;
using System;

namespace Scripts.Services
{
    public interface IWebSocketClient : IDisposable
    {
        WebSocket GetWebSocket { get; }
        bool IsConnected { get; }
        void Update();
    } 
}