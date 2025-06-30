const http = require('http');
const express = require('express');
const path = require('path');
const fs = require('fs');

// Импорт конфигурации и сервисов
const ServerConfig = require('./config/ServerConfig');
const TurnConfig = require('./config/TurnConfig');
const ClientManagerService = require('./services/ClientManagerService');
const WebSocketService = require('./services/WebSocketService');
const WebRTCSignalingService = require('./services/WebRTCSignalingService');
const TurnServerService = require('./services/TurnServerService');
const MessageHandler = require('./handlers/MessageHandler');
const ApiRoutes = require('./routes/ApiRoutes');

class ServerManager {
  constructor(port = ServerConfig.getPort()) {
    this.port = port;
    this.app = express();
    this.server = http.createServer(this.app);
    
    // Проверка WebRTC конфигурации
    this.webrtcConfig = this.loadWebRTCConfig();
    
    // Инициализация сервисов
    this.initializeServices();
    this.setupExpress();
    this.setupGracefulShutdown();
    this.startCleanupTimer();
  }

  loadWebRTCConfig() {
    try {
      const configPath = path.join(__dirname, 'config/webrtc-config.json');
      if (fs.existsSync(configPath)) {
        return JSON.parse(fs.readFileSync(configPath, 'utf8'));
      }
    } catch (error) {
      console.log('⚠️ Не удалось загрузить конфигурацию WebRTC, используем только сигналинг');
    }
    
    return {
      available: false,
      signalingOnly: true
    };
  }

  initializeServices() {
    // Создание сервисов в правильном порядке зависимостей
    this.clientManager = new ClientManagerService();
    
    // TURN Server Service - должен быть инициализирован первым
    this.turnServerService = new TurnServerService();
    
    // WebRTC Signaling Service с TURN поддержкой
    this.webrtcSignalingService = new WebRTCSignalingService(this.clientManager, this.turnServerService);
    
    // MessageHandler зависит от clientManager и webrtcSignalingService
    this.messageHandler = new MessageHandler(
      this.clientManager,
      this.webrtcSignalingService
    );
    
    // WebSocketService зависит от всех предыдущих сервисов
    this.webSocketService = new WebSocketService(
      this.server, 
      this.clientManager, 
      this.messageHandler
    );
    
    // API роуты зависят от всех сервисов
    this.apiRoutes = new ApiRoutes(
      this.clientManager, 
      this.webSocketService,
      this.webrtcSignalingService,
      this.turnServerService
    );
  }

  setupExpress() {
    // CORS middleware для API запросов
    this.app.use((req, res, next) => {
      res.header('Access-Control-Allow-Origin', '*');
      res.header('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
      res.header('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept, Authorization');
      
      // Обработка preflight OPTIONS запросов
      if (req.method === 'OPTIONS') {
        res.sendStatus(200);
      } else {
        next();
      }
    });
    
    // JSON parser middleware
    this.app.use(express.json());
    
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

  startCleanupTimer() {
    // Очистка старых WebRTC сессий каждые 5 минут
    setInterval(() => {
      if (this.webrtcSignalingService) {
        this.webrtcSignalingService.cleanup();
      }
    }, 5 * 60 * 1000);
  }

  start() {
    return new Promise(async (resolve, reject) => {
      try {
        // Сначала запускаем TURN-сервер
        console.log('🔄 Запуск TURN-сервера...');
        const turnStarted = await this.turnServerService.start();
        
        if (turnStarted) {
          console.log('✅ TURN-сервер запущен успешно');
          // Запускаем мониторинг TURN-сервера
          this.turnServerService.startMonitoring();
        } else {
          console.log('⚠️ TURN-сервер не запустился, продолжаем только с STUN');
        }

        // Запускаем основной сервер
        this.server.listen(this.port, (error) => {
          if (error) {
            reject(error);
            return;
          }
          
          console.log(`🚀 WebRTC Сигналинг Сервер запущен на порту ${this.port}`);
          console.log(`📊 Статистика: http://localhost:${this.port}/api/status`);
          
          // Информация о TURN сервере
          const turnStats = this.turnServerService.getStats();
          if (turnStats.isRunning) {
            console.log(`🔄 TURN-сервер: АКТИВЕН на ${TurnConfig.TURN_SERVER_HOST}:${TurnConfig.TURN_SERVER_PORT}`);
            console.log(`🔐 TURN credentials: ${TurnConfig.TURN_USERNAME}:${TurnConfig.TURN_PASSWORD}`);
          } else {
            console.log(`⚠️ TURN-сервер: НЕАКТИВЕН (используется только STUN)`);
          }
          
          // Информация о протоколах
          if (this.webrtcConfig.available) {
            console.log(`🎥 Протокол видео: WebRTC (полная поддержка)`);
            console.log(`📡 WebRTC библиотека: ${this.webrtcConfig.library?.name || 'wrtc'}`);
          } else {
            console.log(`🎥 Протокол видео: WebRTC (только сигналинг)`);
            console.log(`📡 WebRTC сигналинг: Активен`);
          }
          
          console.log(`⚡ Функции: WebRTC сигналинг, TURN сервер, командное управление`);
          console.log(`🏗️  Архитектура: SOLID принципы + WebRTC + TURN`);
          console.log(`🌐 WebRTC сессии: http://localhost:${this.port}/api/webrtc/stats`);
          console.log(`🔄 TURN статистика: http://localhost:${this.port}/api/turn/stats`);
          
          resolve();
        });
        
        this.server.on('error', (error) => {
          console.error('💥 Ошибка HTTP сервера:', error);
          reject(error);
        });
      } catch (error) {
        console.error('💥 Ошибка запуска сервера:', error);
        reject(error);
      }
    });
  }

  shutdown(exitCode = 0) {
    console.log('\n🛑 Получен сигнал остановки...');
    
    // Остановка TURN-сервера
    if (this.turnServerService) {
      this.turnServerService.stop();
    }
    
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

  getWebSocketService() {
    return this.webSocketService;
  }

  getWebRTCSignalingService() {
    return this.webrtcSignalingService;
  }

  getTurnServerService() {
    return this.turnServerService;
  }
}

module.exports = ServerManager; 