const WebSocket = require('ws');

class MessageHandler {
  constructor(clientManager, webrtcSignalingService = null) {
    this.clientManager = clientManager;
    this.webrtcSignalingService = webrtcSignalingService;
  }

  async handleMessage(ws, message) {
    const msgStr = message.toString();
    
    try {
      // Пытаемся парсить как JSON для WebRTC сигналов
      let jsonMessage = null;
      try {
        jsonMessage = JSON.parse(msgStr);
      } catch (e) {
        // Не JSON - обрабатываем как старый формат
      }
      
      // Обработка WebRTC сигналов
      if (jsonMessage && jsonMessage.type === 'webrtc-signal' && this.webrtcSignalingService) {
        return await this.handleWebRTCSignal(ws, jsonMessage);
      }
      
      // Старый формат сообщений (только команды управления)
      const parts = msgStr.split('!');
      const messageType = parts[0];

      // Определяем целевого клиента
      const targetClient = this.clientManager.getTargetClient(ws.clientType);

      // Пересылаем сообщение целевому клиенту
      if (targetClient && targetClient.readyState === WebSocket.OPEN) {
        switch (messageType) {
          case 'COMMAND':
            this.handleCommand(ws, targetClient, msgStr);
            break;
            
          case 'TELEMETRY':
            this.handleTelemetry(ws, targetClient, msgStr);
            break;
            
          default:
            // Другие сообщения просто пересылаем
            targetClient.send(msgStr);
            break;
        }
      } else {
        console.log(`❗ Целевой клиент недоступен (${ws.clientType === 'controller' ? 'робот' : 'контроллер'})`);
        ws.send(`ERROR!TARGET_DISCONNECTED!${ws.clientType === 'controller' ? 'ROBOT' : 'CONTROLLER'}`);
      }
    } catch (error) {
      console.error('💥 Ошибка обработки сообщения:', error);
    }
  }

  /**
   * Обработка WebRTC сигналов
   */
  async handleWebRTCSignal(ws, message) {
    if (!this.webrtcSignalingService) {
      console.log('⚠️ WebRTC сигналинг не активен');
      return false;
    }
    
    const { signalType, data } = message;
    return await this.webrtcSignalingService.handleWebRTCSignal(ws, signalType, data);
  }

  handleCommand(ws, targetClient, message) {
    // Команды управления от контроллера к роботу
    if (ws.clientType === 'controller') {
      targetClient.send(message);
      console.log('📤 Команда переслана роботу');
    }
  }

  handleTelemetry(ws, targetClient, message) {
    // Телеметрия от робота к контроллеру
    if (ws.clientType === 'robot') {
      targetClient.send(message);
    }
  }

  /**
   * Обработка отключения клиента для WebRTC
   */
  handleClientDisconnection(ws) {
    if (this.webrtcSignalingService) {
      this.webrtcSignalingService.handleClientDisconnection(ws);
    }
  }
}

module.exports = MessageHandler; 