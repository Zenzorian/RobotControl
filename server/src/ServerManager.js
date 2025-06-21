const http = require('http');
const express = require('express');
const path = require('path');

// Импорт конфигурации и сервисов
const ServerConfig = require('./config/ServerConfig');
const ClientManagerService = require('./services/ClientManagerService');
const VideoStatsService = require('./services/VideoStatsService');
const WebSocketService = require('./services/WebSocketService');
const MessageHandler = require('./handlers/MessageHandler');
const ApiRoutes = require('./routes/ApiRoutes');

class ServerManager {
  constructor(port = ServerConfig.getPort()) {
    this.port = port;
    this.app = express();
    this.server = http.createServer(this.app);
    
    // Инициализация сервисов
    this.initializeServices();
    this.setupExpress();
    this.setupGracefulShutdown();
  }

  initializeServices() {
    // Создание сервисов в правильном порядке зависимостей
    this.clientManager = new ClientManagerService();
    this.videoStatsService = new VideoStatsService();
    
    // MessageHandler зависит от clientManager и videoStatsService
    this.messageHandler = new MessageHandler(this.clientManager, this.videoStatsService);
    
    // WebSocketService зависит от всех предыдущих сервисов
    this.webSocketService = new WebSocketService(
      this.server, 
      this.clientManager, 
      this.messageHandler, 
      this.videoStatsService
    );
    
    // API роуты зависят от всех сервисов
    this.apiRoutes = new ApiRoutes(
      this.clientManager, 
      this.videoStatsService, 
      this.webSocketService
    );
  }

  setupExpress() {
    // Статические файлы
    this.app.use(express.static(path.join(__dirname, '../public')));
    
    // API роуты
    this.app.use('/api', this.apiRoutes.getRouter());
    
    // Обратная совместимость со старыми эндпоинтами
    this.app.get('/status', (req, res) => {
      res.redirect('/api/status/detailed');
    });
    
    // Обработка ошибок
    this.app.use((err, req, res, next) => {
      console.error('💥 Ошибка Express:', err);
      res.status(500).json({ 
        error: 'Internal Server Error',
        message: err.message 
      });
    });
    
    // 404 обработчик
    this.app.use((req, res) => {
      res.status(404).json({ 
        error: 'Not Found',
        path: req.path 
      });
    });
  }

  setupGracefulShutdown() {
    // Graceful shutdown
    process.on('SIGINT', () => {
      this.shutdown();
    });

    process.on('SIGTERM', () => {
      this.shutdown();
    });

    // Обработка необработанных ошибок
    process.on('uncaughtException', (error) => {
      console.error('💥 Необработанная ошибка:', error);
      this.shutdown(1);
    });

    process.on('unhandledRejection', (reason, promise) => {
      console.error('💥 Необработанное отклонение промиса:', reason);
      this.shutdown(1);
    });
  }

  start() {
    return new Promise((resolve, reject) => {
      this.server.listen(this.port, (error) => {
        if (error) {
          reject(error);
          return;
        }
        
        console.log(`🚀 Оптимизированный сервер запущен на порту ${this.port}`);
        console.log(`📊 Статистика: http://localhost:${this.port}/api/status`);
        console.log(`🎥 Протокол видео: MJPEG over WebSocket`);
        console.log(`⚡ Оптимизации: Низкая задержка, эффективная память`);
        console.log(`🏗️  Архитектура: SOLID принципы, модульная структура`);
        
        resolve();
      });
      
      this.server.on('error', (error) => {
        console.error('💥 Ошибка HTTP сервера:', error);
        reject(error);
      });
    });
  }

  shutdown(exitCode = 0) {
    console.log('\n🛑 Получен сигнал остановки...');
    
    // Остановка WebSocket сервиса
    if (this.webSocketService) {
      this.webSocketService.shutdown();
    }
    
    // Закрытие HTTP сервера
    this.server.close(() => {
      console.log('✅ Сервер остановлен');
      process.exit(exitCode);
    });
    
    // Принудительное завершение через таймаут
    setTimeout(() => {
      console.log('⚠️ Принудительное завершение работы');
      process.exit(exitCode);
    }, ServerConfig.SHUTDOWN_TIMEOUT);
  }

  // Геттеры для доступа к сервисам (если нужно для тестирования)
  getClientManager() {
    return this.clientManager;
  }

  getVideoStatsService() {
    return this.videoStatsService;
  }

  getWebSocketService() {
    return this.webSocketService;
  }
}

module.exports = ServerManager; 