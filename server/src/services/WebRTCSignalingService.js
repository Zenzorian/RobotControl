const { v4: uuidv4 } = require('uuid');

/**
 * WebRTC Signaling Service
 * Следует принципу единственной ответственности (SRP)
 * Отвечает только за WebRTC сигналинг между клиентами
 * Интегрирован с TURN-сервером для NAT traversal
 */
class WebRTCSignalingService {
  constructor(clientManager, turnServerService = null) {
    this.clientManager = clientManager;
    this.turnServerService = turnServerService;
    this.sessions = new Map(); // sessionId -> { robot, controller, state }
    this.stats = {
      sessionsCreated: 0,
      sessionsCompleted: 0,
      sessionsFailed: 0,
      signalsProcessed: 0,
      turnConnectionsUsed: 0
    };
    
    console.log('🎯 WebRTC Signaling Service инициализирован');
    if (this.turnServerService) {
      console.log('🔄 TURN-сервер интегрирован в WebRTC сигналинг');
    }
  }

  /**
   * Обработка WebRTC сигналов
   */
  async handleWebRTCSignal(ws, signalType, data) {
    try {
      this.stats.signalsProcessed++;
      
      console.log(`📡 WebRTC сигнал: ${signalType} от ${ws.clientType}`);
      
      switch (signalType) {
        case 'offer':
          return await this.handleOffer(ws, data);
          
        case 'answer':
          return await this.handleAnswer(ws, data);
          
        case 'ice-candidate':
          return await this.handleIceCandidate(ws, data);
          
        case 'request_video':
          return await this.handleRequestVideo(ws, data);
          
        case 'session-start':
          return await this.handleSessionStart(ws, data);
          
        case 'session-end':
          return await this.handleSessionEnd(ws, data);
          
        case 'ice-configuration':
          return await this.handleICEConfigurationRequest(ws, data);
          
        default:
          console.log(`❓ Неизвестный WebRTC сигнал: ${signalType}`);
          return false;
      }
    } catch (error) {
      console.error(`💥 Ошибка обработки WebRTC сигнала ${signalType}:`, error);
      this.stats.sessionsFailed++;
      return false;
    }
  }

  /**
   * Обработка WebRTC offer от робота
   */
  async handleOffer(ws, data) {
    if (ws.clientType !== 'robot') {
      console.log('❌ Offer может отправлять только робот');
      return false;
    }

    const sessionId = data.sessionId; // Используем sessionId от робота, НЕ генерируем новый!
    
    if (!sessionId) {
      console.log('❌ Offer без sessionId от робота');
      return false;
    }
    
    console.log(`📡 Получен offer от робота с sessionId: ${sessionId}`);
    
    // Создаем новую сессию
    this.sessions.set(sessionId, {
      robot: ws,
      controller: null,
      state: 'offer-created',
      createdAt: Date.now(),
      offer: data
    });
    
    this.stats.sessionsCreated++;
    
    // Пересылаем offer контроллеру (НЕ роботу!)
    const controllerClient = this.clientManager.clients.controller;
    console.log(`🔍 Поиск контроллера для offer: ${controllerClient ? 'найден' : 'не найден'}, readyState: ${controllerClient?.readyState}`);
    
    if (controllerClient && controllerClient.readyState === 1) {
      const offerMessage = {
        type: 'webrtc-signal',
        signalType: 'offer',
        sessionId: sessionId,
        data: data
      };
      
      controllerClient.send(JSON.stringify(offerMessage));
      
      // Обновляем сессию
      const session = this.sessions.get(sessionId);
      session.controller = controllerClient;
      session.state = 'offer-sent';
      
      console.log(`✅ WebRTC offer переслан контроллеру (session: ${sessionId})`);
      return true;
    } else {
      console.log('❌ Контроллер не подключен для WebRTC offer');
      this.sessions.delete(sessionId);
      return false;
    }
  }

  /**
   * Обработка WebRTC answer от контроллера
   */
  async handleAnswer(ws, data) {
    if (ws.clientType !== 'controller') {
      console.log('❌ Answer может отправлять только контроллер');
      return false;
    }

    const sessionId = data.sessionId;
    const session = this.sessions.get(sessionId);
    
    if (!session) {
      console.log(`❌ Сессия не найдена: ${sessionId}`);
      return false;
    }

    // Пересылаем answer роботу
    if (session.robot) {
      const answerMessage = {
        type: 'webrtc-signal',
        signalType: 'answer',
        sessionId: sessionId,
        data: data
      };
      
      session.robot.send(JSON.stringify(answerMessage));
      session.state = 'answer-sent';
      session.answer = data;
      
      console.log(`✅ WebRTC answer переслан роботу (session: ${sessionId})`);
      return true;
    } else {
      console.log('❌ Робот не подключен для WebRTC answer');
      return false;
    }
  }

  /**
   * Обработка ICE кандидатов
   */
  async handleIceCandidate(ws, data) {
    const sessionId = data.sessionId;
    const session = this.sessions.get(sessionId);
    
    if (!session) {
      console.log(`❌ Сессия не найдена для ICE: ${sessionId}`);
      return false;
    }

    // Определяем целевого клиента
    let targetClient = null;
    if (ws.clientType === 'robot' && session.controller) {
      targetClient = session.controller;
    } else if (ws.clientType === 'controller' && session.robot) {
      targetClient = session.robot;
    }

    if (targetClient) {
      // Используем единый новый формат для всех
      const iceMessage = {
        type: 'webrtc-signal',
        signalType: 'ice-candidate',
        sessionId: sessionId,
        data: data
      };
      
      targetClient.send(JSON.stringify(iceMessage));
      console.log(`🧊 ICE кандидат переслан (${ws.clientType} -> ${targetClient.clientType})`);
      return true;
    } else {
      console.log('❌ Целевой клиент не найден для ICE кандидата');
      return false;
    }
  }

  /**
   * Обработка запроса видео от контроллера
   */
  async handleRequestVideo(ws, data) {
    if (ws.clientType !== 'controller') {
      console.log('❌ Запрос видео может отправлять только контроллер');
      return false;
    }

    // Найти подключенного робота
    const robotClient = this.clientManager.getTargetClient('controller');
    console.log(`🔍 Поиск робота для запроса видео...`);
    console.log(`📊 Подключенные клиенты: ${JSON.stringify(this.clientManager.getStats())}`);
    
    if (!robotClient) {
      console.log('❌ Робот не подключен для запроса видео');
      ws.send(JSON.stringify({
        type: 'webrtc-signal',
        signalType: 'error',
        data: { message: 'Robot not connected' }
      }));
      return false;
    }
    
    console.log(`✅ Робот найден: ${robotClient.clientType}, readyState: ${robotClient.readyState}`);

    // Создаем или используем sessionId
    const sessionId = data.sessionId || `session_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    
    console.log(`📹 Обрабатываем запрос видео с sessionId: ${sessionId}`);

    // Переслать запрос роботу с sessionId (новый формат)
    const requestMessage = {
      type: 'webrtc-signal',
      signalType: 'request_video',
      sessionId: sessionId,
      data: data || {}
    };

    try {
      robotClient.send(JSON.stringify(requestMessage));
      console.log(`📹 Запрос видео переслан роботу с sessionId: ${sessionId}`);
      console.log(`📤 Отправленное сообщение: ${JSON.stringify(requestMessage)}`);
    } catch (error) {
      console.log(`❌ Ошибка отправки сообщения роботу: ${error.message}`);
      return false;
    }
    
    return true;
  }

  /**
   * Начало WebRTC сессии
   */
  async handleSessionStart(ws, data) {
    const sessionId = data.sessionId;
    const session = this.sessions.get(sessionId);
    
    if (session) {
      session.state = 'connected';
      session.connectedAt = Date.now();
      console.log(`🎉 WebRTC сессия установлена: ${sessionId}`);
    }
    
    return true;
  }

  /**
   * Завершение WebRTC сессии
   */
  async handleSessionEnd(ws, data) {
    const sessionId = data.sessionId;
    const session = this.sessions.get(sessionId);
    
    if (session) {
      session.state = 'ended';
      session.endedAt = Date.now();
      
      // Уведомляем другого клиента о завершении
      const otherClient = ws.clientType === 'robot' ? session.controller : session.robot;
      if (otherClient) {
        const endMessage = {
          type: 'webrtc-signal',
          signalType: 'session-end',
          sessionId: sessionId,
          data: { reason: 'peer-disconnected' }
        };
        
        otherClient.send(JSON.stringify(endMessage));
      }
      
      this.sessions.delete(sessionId);
      this.stats.sessionsCompleted++;
      
      console.log(`🏁 WebRTC сессия завершена: ${sessionId}`);
    }
    
    return true;
  }

  /**
   * Очистка сессий при отключении клиента
   */
  handleClientDisconnection(ws) {
    // Находим и завершаем все сессии клиента
    for (const [sessionId, session] of this.sessions.entries()) {
      if (session.robot === ws || session.controller === ws) {
        console.log(`🧹 Очистка WebRTC сессии при отключении: ${sessionId}`);
        
        // Уведомляем другого клиента
        const otherClient = session.robot === ws ? session.controller : session.robot;
        if (otherClient) {
          const endMessage = {
            type: 'webrtc-signal',
            signalType: 'session-end',
            sessionId: sessionId,
            data: { reason: 'peer-disconnected' }
          };
          
          try {
            otherClient.send(JSON.stringify(endMessage));
          } catch (error) {
            // Клиент уже отключен
          }
        }
        
        this.sessions.delete(sessionId);
      }
    }
  }

  /**
   * Получение статистики WebRTC
   */
  getStats() {
    const activeSessions = Array.from(this.sessions.values()).filter(
      session => session.state === 'connected'
    ).length;
    
    return {
      activeSessions,
      totalSessions: this.sessions.size,
      stats: {
        ...this.stats,
        activeSessionsCount: activeSessions
      },
      sessions: Array.from(this.sessions.entries()).map(([id, session]) => ({
        id,
        state: session.state,
        createdAt: session.createdAt,
        connectedAt: session.connectedAt,
        duration: session.connectedAt ? Date.now() - session.connectedAt : 0
      }))
    };
  }

  /**
   * Очистка старых сессий
   */
  cleanup() {
    const now = Date.now();
    const maxAge = 5 * 60 * 1000; // 5 минут
    
    for (const [sessionId, session] of this.sessions.entries()) {
      if (now - session.createdAt > maxAge && session.state !== 'connected') {
        console.log(`🧹 Удаление старой WebRTC сессии: ${sessionId}`);
        this.sessions.delete(sessionId);
      }
    }
  }

  /**
   * Получение ICE конфигурации для клиентов
   * Включает TURN серверы если доступны
   */
  getICEConfiguration() {
    if (this.turnServerService) {
      const config = this.turnServerService.getICEConfiguration();
      console.log('🧊 Отправка ICE конфигурации с TURN сервером');
      return config;
    } else {
      // Fallback только на STUN серверы
      console.log('🧊 Отправка ICE конфигурации только с STUN серверами');
      return {
        iceServers: [
          { urls: 'stun:stun.l.google.com:19302' },
          { urls: 'stun:stun1.l.google.com:19302' }
        ],
        iceCandidatePoolSize: 10,
        bundlePolicy: 'max-bundle',
        rtcpMuxPolicy: 'require'
      };
    }
  }

  /**
   * Отправка ICE конфигурации клиенту
   */
  async sendICEConfiguration(ws) {
    try {
      const iceConfig = this.getICEConfiguration();
      const iceMessage = {
        type: 'webrtc-signal',
        signalType: 'ice-configuration',
        data: iceConfig
      };

      ws.send(JSON.stringify(iceMessage));
      console.log(`🧊 ICE конфигурация отправлена ${ws.clientType}`);
      return true;
    } catch (error) {
      console.error('❌ Ошибка отправки ICE конфигурации:', error);
      return false;
    }
  }

  /**
   * Обработка запроса ICE конфигурации
   */
  async handleICEConfigurationRequest(ws, data) {
    console.log(`🧊 Запрос ICE конфигурации от ${ws.clientType}`);
    return await this.sendICEConfiguration(ws);
  }

  /**
   * Получение статистики TURN сервера
   */
  getTurnStats() {
    if (this.turnServerService) {
      const turnStats = this.turnServerService.getStats();
      return {
        turnServerAvailable: true,
        ...turnStats,
        connectionsUsedInWebRTC: this.stats.turnConnectionsUsed
      };
    } else {
      return {
        turnServerAvailable: false,
        message: 'TURN сервер не инициализирован'
      };
    }
  }
}

module.exports = WebRTCSignalingService; 