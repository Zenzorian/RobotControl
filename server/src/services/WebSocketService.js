const WebSocket = require('ws');
const ServerConfig = require('../config/ServerConfig');

class WebSocketService {
  constructor(server, clientManager, messageHandler) {
    this.wss = new WebSocket.Server({ server });
    this.clientManager = clientManager;
    this.messageHandler = messageHandler;
    
    this.setupWebSocketServer();
    this.startHealthCheck();
  }

  setupWebSocketServer() {
    this.wss.on('connection', (ws) => {
      console.log('📡 Новое подключение');

      // Ожидаем регистрацию клиента
      ws.once('message', (message) => {
        this.handleRegistration(ws, message);
      });

      // Обработка отключения
      ws.on('close', () => {
        this.handleDisconnection(ws);
      });

      ws.on('error', (error) => {
        console.error('💥 Ошибка WebSocket:', error);
      });
    });

    this.wss.on('error', (error) => {
      console.error('💥 Ошибка WebSocket сервера:', error);
    });
  }

  handleRegistration(ws, message) {
    const msgStr = message.toString();
    const parts = msgStr.split('!');
    
    if (parts[0] === 'REGISTER' && parts[1]) {
      const response = this.clientManager.registerClient(ws, parts[1]);
      
      if (response) {
        ws.send(response);
        this.setupMessageHandler(ws);
      } else {
        console.log('❓ Неизвестный тип клиента');
        ws.close();
      }
    } else {
      console.log('❓ Неверный формат регистрации');
      ws.close();
    }
  }

  handleDisconnection(ws) {
    // Уведомляем MessageHandler об отключении для WebRTC
    if (this.messageHandler.handleClientDisconnection) {
      this.messageHandler.handleClientDisconnection(ws);
    }
    
    // Стандартная обработка отключения
    this.clientManager.unregisterClient(ws);
  }

  setupMessageHandler(ws) {
    ws.on('message', async (message) => {
      await this.messageHandler.handleMessage(ws, message);
    });
  }

  startHealthCheck() {
    // Мониторинг соединений
    setInterval(() => {
      // Ping активных клиентов
      this.wss.clients.forEach((client) => {
        if (client.readyState === WebSocket.OPEN) {
          client.ping();
        }
      });
    }, ServerConfig.HEALTH_CHECK_INTERVAL);
  }

  getActiveConnectionsCount() {
    return Array.from(this.wss.clients).filter(
      client => client.readyState === WebSocket.OPEN
    ).length;
  }

  shutdown() {
    console.log('🛑 Закрытие WebSocket соединений...');
    
    // Уведомляем всех клиентов о завершении работы
    this.wss.clients.forEach((client) => {
      if (client.readyState === WebSocket.OPEN) {
        client.send('SERVER_SHUTDOWN');
        client.close();
      }
    });
  }
}

module.exports = WebSocketService; 