const express = require('express');

class ApiRoutes {
  constructor(clientManager, videoStatsService, webSocketService) {
    this.router = express.Router();
    this.clientManager = clientManager;
    this.videoStatsService = videoStatsService;
    this.webSocketService = webSocketService;
    
    this.setupRoutes();
  }

  setupRoutes() {
    // Основной статус сервера
    this.router.get('/status', (req, res) => {
      res.json({
        server: 'Optimized Robot Control Server',
        timestamp: new Date().toISOString(),
        connections: this.clientManager.getConnectionsStatus(),
        video: this.videoStatsService.getDetailedStats()
      });
    });

    // Детальная статистика
    this.router.get('/status/detailed', (req, res) => {
      res.json({
        websocket: {
          connections: this.webSocketService.getActiveConnectionsCount(),
          ...this.clientManager.getConnectionsStatus()
        },
        video: this.videoStatsService.getDetailedStats(),
        performance: {
          memoryUsage: process.memoryUsage(),
          cpuUsage: process.cpuUsage()
        }
      });
    });

    // Статистика видео
    this.router.get('/video/stats', (req, res) => {
      res.json(this.videoStatsService.getStats());
    });

    // Управление видео
    this.router.post('/video/control', express.json(), (req, res) => {
      const { action } = req.body;
      
      if (action === 'start') {
        if (this.clientManager.sendToClient('robot', 'REQUEST_VIDEO_STREAM')) {
          res.json({ success: true, message: 'Video stream start requested' });
        } else {
          res.status(400).json({ success: false, message: 'Robot not connected' });
        }
      } else if (action === 'stop') {
        if (this.clientManager.sendToClient('robot', 'STOP_VIDEO_STREAM')) {
          res.json({ success: true, message: 'Video stream stop requested' });
        } else {
          res.status(400).json({ success: false, message: 'Robot not connected' });
        }
      } else {
        res.status(400).json({ success: false, message: 'Invalid action. Use "start" or "stop"' });
      }
    });

    // Информация о подключениях
    this.router.get('/connections', (req, res) => {
      res.json({
        active: this.webSocketService.getActiveConnectionsCount(),
        clients: this.clientManager.getConnectionsStatus()
      });
    });

    // Health check
    this.router.get('/health', (req, res) => {
      res.json({
        status: 'healthy',
        timestamp: new Date().toISOString(),
        uptime: process.uptime()
      });
    });
  }

  getRouter() {
    return this.router;
  }
}

module.exports = ApiRoutes; 