// Пример unit-тестов для новой архитектуры
// Для запуска тестов нужно установить: npm install --save-dev jest

const ServerManager = require('../ServerManager');
const ClientManagerService = require('../services/ClientManagerService');
const VideoStatsService = require('../services/VideoStatsService');

describe('ServerManager', () => {
  let serverManager;

  beforeEach(() => {
    serverManager = new ServerManager(0); // Используем порт 0 для тестов
  });

  afterEach(async () => {
    if (serverManager) {
      serverManager.shutdown();
    }
  });

  test('should initialize all services', () => {
    expect(serverManager.getClientManager()).toBeInstanceOf(ClientManagerService);
    expect(serverManager.getVideoStatsService()).toBeInstanceOf(VideoStatsService);
    expect(serverManager.getWebSocketService()).toBeDefined();
  });

  test('should start server successfully', async () => {
    await expect(serverManager.start()).resolves.toBeUndefined();
  });
});

describe('ClientManagerService', () => {
  let clientManager;

  beforeEach(() => {
    clientManager = new ClientManagerService();
  });

  test('should register controller client', () => {
    const mockWs = { clientType: null };
    const response = clientManager.registerClient(mockWs, 'CONTROLLER');
    
    expect(response).toBe('REGISTERED!CONTROLLER');
    expect(mockWs.clientType).toBe('controller');
  });

  test('should register robot client', () => {
    const mockWs = { clientType: null };
    const response = clientManager.registerClient(mockWs, 'ROBOT');
    
    expect(response).toBe('REGISTERED!ROBOT');
    expect(mockWs.clientType).toBe('robot');
  });

  test('should return null for unknown client type', () => {
    const mockWs = { clientType: null };
    const response = clientManager.registerClient(mockWs, 'UNKNOWN');
    
    expect(response).toBeNull();
  });

  test('should get connections status', () => {
    const status = clientManager.getConnectionsStatus();
    
    expect(status).toEqual({
      controller: 'disconnected',
      robot: 'disconnected'
    });
  });
});

describe('VideoStatsService', () => {
  let videoStats;

  beforeEach(() => {
    videoStats = new VideoStatsService();
  });

  test('should initialize with default stats', () => {
    const stats = videoStats.getStats();
    
    expect(stats.isStreaming).toBe(false);
    expect(stats.frameCount).toBe(0);
    expect(stats.actualFPS).toBe(0);
  });

  test('should start streaming', () => {
    videoStats.startStreaming();
    const stats = videoStats.getStats();
    
    expect(stats.isStreaming).toBe(true);
    expect(stats.startTime).toBeDefined();
  });

  test('should stop streaming', () => {
    videoStats.startStreaming();
    videoStats.stopStreaming();
    const stats = videoStats.getStats();
    
    expect(stats.isStreaming).toBe(false);
  });

  test('should update frame stats', () => {
    const frameData = { frame_number: 1 };
    videoStats.updateFrameStats(frameData);
    const stats = videoStats.getStats();
    
    expect(stats.frameCount).toBe(1);
    expect(stats.lastFrameTime).toBeDefined();
  });

  test('should determine when to log frame', () => {
    // Первые 29 кадров не должны логироваться
    for (let i = 1; i < 30; i++) {
      videoStats.updateFrameStats({ frame_number: i });
      expect(videoStats.shouldLogFrame()).toBe(false);
    }
    
    // 30-й кадр должен логироваться
    videoStats.updateFrameStats({ frame_number: 30 });
    expect(videoStats.shouldLogFrame()).toBe(true);
  });
});

// Для запуска тестов:
// npm install --save-dev jest
// npx jest src/tests/ 