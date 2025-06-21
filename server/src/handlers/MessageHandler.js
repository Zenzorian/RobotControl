const WebSocket = require('ws');

class MessageHandler {
  constructor(clientManager, videoStatsService) {
    this.clientManager = clientManager;
    this.videoStatsService = videoStatsService;
  }

  async handleMessage(ws, message) {
    const msgStr = message.toString();
    const parts = msgStr.split('!');
    const messageType = parts[0];
    
    try {
      // Обработка видео кадров от робота
      if (messageType === 'VIDEO_FRAME' && ws.clientType === 'robot') {
        await this.handleVideoFrame(parts[1]);
        return;
      }

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
            
          case 'REQUEST_VIDEO_STREAM':
            this.handleVideoStreamRequest(ws, targetClient, msgStr);
            break;
            
          case 'STOP_VIDEO_STREAM':
            this.handleVideoStreamStop(ws, targetClient, msgStr);
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

  handleVideoStreamRequest(ws, targetClient, message) {
    console.log(`📹 Запрос видео от ${ws.clientType}`);
    if (ws.clientType === 'controller' && targetClient) {
      targetClient.send(message);
      this.videoStatsService.startStreaming();
    }
  }

  handleVideoStreamStop(ws, targetClient, message) {
    console.log(`📹 Остановка видео от ${ws.clientType}`);
    if (targetClient) {
      targetClient.send(message);
    }
    this.videoStatsService.stopStreaming();
  }

  async handleVideoFrame(frameDataStr) {
    try {
      const frameData = JSON.parse(frameDataStr);
      
      // Обновляем статистику
      this.videoStatsService.updateFrameStats(frameData);
      
      // Пересылаем кадр контроллеру
      if (this.clientManager.isClientConnected('controller')) {
        const message = `VIDEO_FRAME!${frameDataStr}`;
        this.clientManager.sendToClient('controller', message);
        
        // Логируем каждый 30-й кадр для мониторинга
        if (this.videoStatsService.shouldLogFrame()) {
          const stats = this.videoStatsService.getStats();
          console.log(`📺 Кадр #${frameData.frame_number} переслан контроллеру (FPS: ${stats.actualFPS.toFixed(1)})`);
        }
      }
      
    } catch (error) {
      console.error('💥 Ошибка обработки видео кадра:', error);
    }
  }
}

module.exports = MessageHandler; 