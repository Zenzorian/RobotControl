const express = require('express');

class ApiRoutes {
  constructor(clientManager, webSocketService, webrtcSignalingService = null) {
    this.router = express.Router();
    this.clientManager = clientManager;
    this.webSocketService = webSocketService;
    this.webrtcSignalingService = webrtcSignalingService;
    
    this.setupRoutes();
  }

  setupRoutes() {
    // Основной статус сервера
    this.router.get('/status', (req, res) => {
      const response = {
        server: 'WebRTC Signaling Server for Robot Control',
        timestamp: new Date().toISOString(),
        connections: this.clientManager.getConnectionsStatus()
      };
      
      // Добавляем WebRTC статус если доступен
      if (this.webrtcSignalingService) {
        response.webrtc = {
          enabled: true,
          activeSessions: this.webrtcSignalingService.getStats().activeSessions
        };
      }
      
      res.json(response);
    });

    // Детальная статистика
    this.router.get('/status/detailed', (req, res) => {
      const response = {
        websocket: {
          connections: this.webSocketService.getActiveConnectionsCount(),
          ...this.clientManager.getConnectionsStatus()
        },
        performance: {
          memoryUsage: process.memoryUsage(),
          cpuUsage: process.cpuUsage()
        }
      };
      
      // Добавляем детальную WebRTC статистику
      if (this.webrtcSignalingService) {
        response.webrtc = this.webrtcSignalingService.getStats();
      }
      
      res.json(response);
    });

    // WebRTC статистика
    this.router.get('/webrtc/stats', (req, res) => {
      if (this.webrtcSignalingService) {
        res.json(this.webrtcSignalingService.getStats());
      } else {
        res.status(503).json({ 
          error: 'WebRTC signaling service not available' 
        });
      }
    });

    // WebRTC сессии
    this.router.get('/webrtc/sessions', (req, res) => {
      if (this.webrtcSignalingService) {
        const stats = this.webrtcSignalingService.getStats();
        res.json({
          activeSessions: stats.activeSessions,
          totalSessions: stats.totalSessions,
          sessions: stats.sessions
        });
      } else {
        res.status(503).json({ 
          error: 'WebRTC signaling service not available' 
        });
      }
    });

    // Информация о подключениях
    this.router.get('/connections', (req, res) => {
      const response = {
        active: this.webSocketService.getActiveConnectionsCount(),
        clients: this.clientManager.getConnectionsStatus()
      };
      
      // Добавляем информацию о WebRTC соединениях
      if (this.webrtcSignalingService) {
        const webrtcStats = this.webrtcSignalingService.getStats();
        response.webrtc = {
          activeSessions: webrtcStats.activeSessions,
          totalSessions: webrtcStats.totalSessions
        };
      }
      
      res.json(response);
    });

    // Health check
    this.router.get('/health', (req, res) => {
      const health = {
        status: 'healthy',
        timestamp: new Date().toISOString(),
        uptime: process.uptime(),
        services: {
          websocket: true,
          webrtc: !!this.webrtcSignalingService
        }
      };
      
      res.json(health);
    });

    // WebRTC конфигурация для клиентов
    this.router.get('/webrtc/config', (req, res) => {
      res.json({
        enabled: !!this.webrtcSignalingService,
        signalingEndpoint: '/api/webrtc/signal',
        iceServers: [
          { urls: 'stun:stun.l.google.com:19302' },
          { urls: 'stun:stun1.l.google.com:19302' }
        ]
      });
    });
  }

  getRouter() {
    return this.router;
  }
}

module.exports = ApiRoutes; 