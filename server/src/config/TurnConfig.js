class TurnConfig {
  static get TURN_SERVER_PORT() {
    return process.env.TURN_PORT || 3478;
  }

  static get TURN_SERVER_TLS_PORT() {
    return process.env.TURN_TLS_PORT || 5349;
  }

  static get TURN_SERVER_HOST() {
    return process.env.TURN_HOST || '193.169.240.11';
  }

  static get TURN_USERNAME() {
    return process.env.TURN_USERNAME || 'robotclient';
  }

  static get TURN_PASSWORD() {
    return process.env.TURN_PASSWORD || 'robotclient2024';
  }

  static get TURN_REALM() {
    return process.env.TURN_REALM || 'robotclient.local';
  }

  static get TURN_SECRET() {
    return process.env.TURN_SECRET || 'robotclient-secret-2024';
  }

  // Публичные STUN серверы как fallback
  static get PUBLIC_STUN_SERVERS() {
    return [
      'stun:stun.l.google.com:19302',
      'stun:stun1.l.google.com:19302',
      'stun:stun2.l.google.com:19302',
      'stun:stun.cloudflare.com:3478'
    ];
  }

  // Полная ICE конфигурация для WebRTC
  static getICEConfiguration() {
    const iceServers = [];

    // Добавляем STUN серверы
    this.PUBLIC_STUN_SERVERS.forEach(stunServer => {
      iceServers.push({ urls: stunServer });
    });

    // Добавляем наш TURN сервер (UDP)
    iceServers.push({
      urls: `turn:${this.TURN_SERVER_HOST}:${this.TURN_SERVER_PORT}`,
      username: this.TURN_USERNAME,
      credential: this.TURN_PASSWORD
    });

    // Добавляем TURN сервер (TCP)
    iceServers.push({
      urls: `turn:${this.TURN_SERVER_HOST}:${this.TURN_SERVER_PORT}?transport=tcp`,
      username: this.TURN_USERNAME,
      credential: this.TURN_PASSWORD
    });

    // Добавляем TURNS сервер (TLS)
    iceServers.push({
      urls: `turns:${this.TURN_SERVER_HOST}:${this.TURN_SERVER_TLS_PORT}`,
      username: this.TURN_USERNAME,
      credential: this.TURN_PASSWORD
    });

    return {
      iceServers,
      iceCandidatePoolSize: 10,
      bundlePolicy: 'max-bundle',
      rtcpMuxPolicy: 'require'
    };
  }

  // Конфигурация для coturn сервера
  static getCoturnConfig() {
    return {
      'listening-port': this.TURN_SERVER_PORT,
      'tls-listening-port': this.TURN_SERVER_TLS_PORT,
      'listening-ip': '0.0.0.0',
      'external-ip': this.TURN_SERVER_HOST,
      'relay-ip': this.TURN_SERVER_HOST,
      'fingerprint': true,
      'lt-cred-mech': true,
      'user': `${this.TURN_USERNAME}:${this.TURN_PASSWORD}`,
      'realm': this.TURN_REALM,
      'server-name': this.TURN_REALM,
      'total-quota': 100,
      'stale-nonce': true,
      'cert': '/etc/ssl/certs/turn-server.crt',
      'pkey': '/etc/ssl/private/turn-server.key',
      'log-file': '/var/log/turnserver.log',
      'verbose': true
    };
  }

  // Проверка доступности TURN сервера
  static async checkTurnServerHealth() {
    const net = require('net');
    
    return new Promise((resolve) => {
      const socket = new net.Socket();
      const timeout = setTimeout(() => {
        socket.destroy();
        resolve(false);
      }, 5000);

      socket.connect(this.TURN_SERVER_PORT, this.TURN_SERVER_HOST, () => {
        clearTimeout(timeout);
        socket.destroy();
        resolve(true);
      });

      socket.on('error', () => {
        clearTimeout(timeout);
        resolve(false);
      });
    });
  }
}

module.exports = TurnConfig; 