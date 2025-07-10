using System;

namespace Scripts.Services.RobotVideoProcessing
{
    /// <summary>
    /// Базовый класс для WebRTC сообщений
    /// </summary>
    [Serializable]
    public class WebRTCMessage
    {
        public string type;
        public string signalType;
        public string sessionId;
    }

    /// <summary>
    /// WebRTC Offer сообщение
    /// </summary>
    [Serializable]
    public class WebRTCOfferMessage : WebRTCMessage
    {
        public WebRTCOfferData data;
    }

    /// <summary>
    /// WebRTC Answer сообщение
    /// </summary>
    [Serializable]
    public class WebRTCAnswerMessage : WebRTCMessage
    {
        public WebRTCAnswerData data;
    }

    /// <summary>
    /// WebRTC ICE кандидат сообщение
    /// </summary>
    [Serializable]
    public class WebRTCIceCandidateMessage : WebRTCMessage
    {
        public WebRTCIceCandidateData data;
    }

    /// <summary>
    /// Данные WebRTC Offer
    /// </summary>
    [Serializable]
    public class WebRTCOfferData
    {
        public string sdp;
        public string type;
        public string sessionId;
    }

    /// <summary>
    /// Данные WebRTC Answer
    /// </summary>
    [Serializable]
    public class WebRTCAnswerData
    {
        public string sdp;
        public string type;
        public string sessionId;
    }

    /// <summary>
    /// Данные ICE кандидата
    /// </summary>
    [Serializable]
    public class WebRTCIceCandidateData
    {
        public string candidate;
        public string sdpMid;
        public int sdpMLineIndex;
        public string sessionId;
    }

    /// <summary>
    /// Запрос видео потока
    /// </summary>
    [Serializable]
    public class VideoRequestMessage
    {
        public string type = "video-request";
        public string action = "start";
        public VideoRequestSettings settings;
    }

    /// <summary>
    /// Настройки видео запроса
    /// </summary>
    [Serializable]
    public class VideoRequestSettings
    {
        public int width = 640;
        public int height = 480;
        public int fps = 30;
        public string codec = "h264";
    }

    /// <summary>
    /// Статус видео соединения
    /// </summary>
    [Serializable]
    public class VideoStatusMessage
    {
        public string type = "video-status";
        public string status;
        public string sessionId;
        public string error;
    }
} 