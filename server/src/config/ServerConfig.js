class ServerConfig {
  static get DEFAULT_PORT() {
    return 8080;
  }

  static get HEALTH_CHECK_INTERVAL() {
    return 30000; // 30 секунд
  }

  static get SHUTDOWN_TIMEOUT() {
    return 10000; // 10 секунд
  }

  static get VIDEO_FRAME_LOG_INTERVAL() {
    return 30; // Логировать каждый 30-й кадр
  }

  static get FPS_UPDATE_INTERVAL() {
    return 1000; // 1 секунда
  }

  static getPort() {
    return process.env.PORT || this.DEFAULT_PORT;
  }

  static isProduction() {
    return process.env.NODE_ENV === 'production';
  }

  static isDevelopment() {
    return process.env.NODE_ENV === 'development';
  }

  static getLogLevel() {
    return process.env.LOG_LEVEL || (this.isProduction() ? 'info' : 'debug');
  }
}

module.exports = ServerConfig; 