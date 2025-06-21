const ServerConfig = require('../config/ServerConfig');

class VideoStatsService {
  constructor() {
    this.stats = {
      isStreaming: false,
      frameCount: 0,
      startTime: null,
      lastFrameTime: null,
      actualFPS: 0,
      fpsCounter: 0,
      lastFPSTime: Date.now()
    };
  }

  startStreaming() {
    this.stats.isStreaming = true;
    this.stats.startTime = Date.now();
    console.log('🎥 Видео стриминг запущен');
  }

  stopStreaming() {
    this.stats.isStreaming = false;
    console.log('🛑 Видео стриминг остановлен');
  }

  updateFrameStats(frameData) {
    const now = Date.now();
    
    this.stats.frameCount++;
    this.stats.lastFrameTime = now;
    this.stats.fpsCounter++;
    
    // Подсчет реального FPS каждую секунду
    if (now - this.stats.lastFPSTime >= ServerConfig.FPS_UPDATE_INTERVAL) {
      const elapsed = (now - this.stats.lastFPSTime) / 1000;
      this.stats.actualFPS = this.stats.fpsCounter / elapsed;
      this.stats.fpsCounter = 0;
      this.stats.lastFPSTime = now;
    }
  }

  getStats() {
    return { ...this.stats };
  }

  getDetailedStats() {
    const uptime = this.stats.startTime ? (Date.now() - this.stats.startTime) / 1000 : 0;
    
    return {
      enabled: true,
      protocol: 'MJPEG over WebSocket',
      streaming: this.stats.isStreaming,
      frameCount: this.stats.frameCount,
      fps: Math.round(this.stats.actualFPS * 10) / 10,
      uptime: Math.round(uptime),
      lastFrame: this.stats.lastFrameTime ? new Date(this.stats.lastFrameTime).toISOString() : null
    };
  }

  shouldLogFrame() {
    return this.stats.frameCount % ServerConfig.VIDEO_FRAME_LOG_INTERVAL === 0;
  }
}

module.exports = VideoStatsService; 