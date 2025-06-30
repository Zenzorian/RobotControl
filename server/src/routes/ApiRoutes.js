const express = require('express');

class ApiRoutes {
  constructor(clientManager, webSocketService, webrtcSignalingService = null, turnServerService = null) {
    this.router = express.Router();
    this.clientManager = clientManager;
    this.webSocketService = webSocketService;
    this.webrtcSignalingService = webrtcSignalingService;
    this.turnServerService = turnServerService;
    
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

    // TURN Server статистика
    this.router.get('/turn/stats', (req, res) => {
      if (this.turnServerService) {
        res.json(this.turnServerService.getStats());
      } else {
        res.status(503).json({ 
          error: 'TURN server service not available' 
        });
      }
    });

    // TURN Server управление
    this.router.post('/turn/restart', async (req, res) => {
      if (this.turnServerService) {
        try {
          const result = await this.turnServerService.restart();
          res.json({ 
            success: result,
            message: result ? 'TURN server restarted successfully' : 'Failed to restart TURN server'
          });
        } catch (error) {
          res.status(500).json({ 
            error: 'Failed to restart TURN server',
            details: error.message 
          });
        }
      } else {
        res.status(503).json({ 
          error: 'TURN server service not available' 
        });
      }
    });

    // ICE конфигурация для клиентов
    this.router.get('/ice/config', (req, res) => {
      if (this.webrtcSignalingService) {
        const iceConfig = this.webrtcSignalingService.getICEConfiguration();
        res.json(iceConfig);
      } else {
        // Fallback конфигурация
        res.json({
          iceServers: [
            { urls: 'stun:stun.l.google.com:19302' },
            { urls: 'stun:stun1.l.google.com:19302' }
          ]
        });
      }
    });

    // Health check
    this.router.get('/health', (req, res) => {
      const health = {
        status: 'healthy',
        timestamp: new Date().toISOString(),
        uptime: process.uptime(),
        services: {
          websocket: true,
          webrtc: !!this.webrtcSignalingService,
          turn: !!this.turnServerService && this.turnServerService.getStats().isRunning
        }
      };
      
      res.json(health);
    });

    // WebRTC конфигурация для клиентов (обновленная с TURN)
    this.router.get('/webrtc/config', (req, res) => {
      const config = {
        enabled: !!this.webrtcSignalingService,
        signalingEndpoint: '/api/webrtc/signal',
        iceConfigEndpoint: '/api/ice/config'
      };

      if (this.webrtcSignalingService) {
        config.iceServers = this.webrtcSignalingService.getICEConfiguration().iceServers;
      } else {
        config.iceServers = [
          { urls: 'stun:stun.l.google.com:19302' },
          { urls: 'stun:stun1.l.google.com:19302' }
        ];
      }

      res.json(config);
    });
  }

  getRouter() {
    return this.router;
  }
}

module.exports = ApiRoutes; 