// Пример unit-тестов для новой архитектуры
// Для запуска тестов нужно установить: npm install --save-dev jest

const ServerManager = require('../ServerManager');
const ClientManagerService = require('../services/ClientManagerService');
const WebSocketService = require('../services/WebSocketService');
const WebRTCSignalingService = require('../services/WebRTCSignalingService');

describe('ServerManager', () => {
  let serverManager;
  
  beforeEach(() => {
    serverManager = new ServerManager(0); // Используем случайный порт для тестов
  });
  
  afterEach(async () => {
    if (serverManager) {
      await serverManager.shutdown();
    }
  });

  describe('Service Initialization', () => {
    test('should initialize all required services', () => {
      expect(serverManager.getClientManager()).toBeInstanceOf(ClientManagerService);
      expect(serverManager.getWebSocketService()).toBeInstanceOf(WebSocketService);
      expect(serverManager.getWebRTCSignalingService()).toBeInstanceOf(WebRTCSignalingService);
    });

    test('should have proper service dependencies', () => {
      const clientManager = serverManager.getClientManager();
      const webSocketService = serverManager.getWebSocketService();
      const webrtcService = serverManager.getWebRTCSignalingService();
      
      expect(clientManager).toBeDefined();
      expect(webSocketService).toBeDefined();
      expect(webrtcService).toBeDefined();
    });
  });

  describe('Configuration Loading', () => {
    test('should load WebRTC configuration', () => {
      expect(serverManager.webrtcConfig).toBeDefined();
      expect(typeof serverManager.webrtcConfig.signalingOnly).toBe('boolean');
    });
  });

  describe('Express Setup', () => {
    test('should setup Express application', () => {
      expect(serverManager.app).toBeDefined();
      expect(serverManager.server).toBeDefined();
    });
  });
});

describe('WebRTCSignalingService', () => {
  let clientManager;
  let webrtcService;
  
  beforeEach(() => {
    clientManager = new ClientManagerService();
    webrtcService = new WebRTCSignalingService(clientManager);
  });

  describe('Initialization', () => {
    test('should initialize with proper dependencies', () => {
      expect(webrtcService.clientManager).toBe(clientManager);
      expect(webrtcService.sessions).toBeInstanceOf(Map);
      expect(webrtcService.stats).toBeDefined();
    });

    test('should have empty sessions initially', () => {
      const stats = webrtcService.getStats();
      expect(stats.activeSessions).toBe(0);
      expect(stats.totalSessions).toBe(0);
    });
  });

  describe('Statistics', () => {
    test('should provide proper statistics structure', () => {
      const stats = webrtcService.getStats();
      expect(stats).toHaveProperty('activeSessions');
      expect(stats).toHaveProperty('totalSessions');
      expect(stats).toHaveProperty('stats');
      expect(stats).toHaveProperty('sessions');
    });

    test('should track sessions correctly', () => {
      const stats = webrtcService.getStats();
      expect(stats.stats.sessionsCreated).toBe(0);
      expect(stats.stats.sessionsCompleted).toBe(0);
      expect(stats.stats.signalsProcessed).toBe(0);
    });
  });

  describe('Session Management', () => {
    test('should cleanup old sessions', () => {
      webrtcService.cleanup();
      const stats = webrtcService.getStats();
      expect(stats.activeSessions).toBe(0);
    });
  });
});

// Для запуска тестов:
// npm install --save-dev jest
// npx jest src/tests/ 