using System;
using UnityEngine;

namespace Scripts.Services
{
    public interface IWebRTCVideoService
    {
        // Свойства состояния
        bool IsConnected { get; }
        bool IsStreaming { get; }
        string ConnectionState { get; }
        string CurrentSessionId { get; }
        
        // Статистика
        float CurrentFPS { get; }
        int ReceivedFrames { get; }
        long BytesReceived { get; }
        TimeSpan ConnectionTime { get; }
        
        // События
        event Action<bool> OnConnectionStateChanged;
        event Action<string> OnError;
        
        // Управление
        void Initialize(IWebSocketClient webSocketClient);
        void StartConnection();
        void StopConnection();
        void SetVideoOutput(UnityEngine.UI.RawImage videoOutput);
        
        // Диагностика
        string GetStatusReport();
        void ResetStatistics();
        
        // WebRTC сигналинг
        void HandleWebRTCSignal(string signalType, string data);
    }
} 