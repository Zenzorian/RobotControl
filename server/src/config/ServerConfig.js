class ServerConfig {
  static get DEFAULT_PORT() {
    return 80;
  }

  static get HEALTH_CHECK_INTERVAL() {
    return 30000; // 30 секунд
  }

  static get SHUTDOWN_TIMEOUT() {
    return 10000; // 10 секунд
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