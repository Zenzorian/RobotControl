class TurnConfig {
  static get TURN_SERVER_PORT() {
    return process.env.TURN_PORT || 13478; // Непривилегированный порт
  }

  static get TURN_SERVER_TLS_PORT() {
    return process.env.TURN_TLS_PORT || 15349; // Непривилегированный порт
  }

  static get TURN_SERVER_HOST() {
    if (process.env.TURN_HOST) {
      return process.env.TURN_HOST;
    }
    
    const os = require('os');
    const networkInterfaces = os.networkInterfaces();
    
    for (const interfaceName in networkInterfaces) {
      const interfaces = networkInterfaces[interfaceName];
      for (const iface of interfaces) {
        if (iface.family === 'IPv4' && !iface.internal) {
          console.log(`🔧 Автоопределение TURN IP: ${iface.address} (интерфейс: ${interfaceName})`);
          return iface.address;
        }
      }
    }
    
    console.log(`⚠️ Внешний IP не найден, используем localhost для TURN`);
    return '127.0.0.1';
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

  static get PUBLIC_STUN_SERVERS() {
    return [
      'stun:stun.l.google.com:19302',
      'stun:stun1.l.google.com:19302',
      'stun:stun2.l.google.com:19302',
      'stun:stun.cloudflare.com:3478'
    ];
  }

  static getICEConfiguration() {
    const iceServers = [];

    this.PUBLIC_STUN_SERVERS.forEach(stunServer => {
      iceServers.push({ urls: stunServer });
    });

    const turnHost = this.TURN_SERVER_HOST;
    console.log(`🔄 Генерация ICE конфигурации с TURN IP: ${turnHost}`);

    iceServers.push({
      urls: `turn:${turnHost}:${this.TURN_SERVER_PORT}`,
      username: this.TURN_USERNAME,
      credential: this.TURN_PASSWORD
    });

    iceServers.push({
      urls: `turn:${turnHost}:${this.TURN_SERVER_PORT}?transport=tcp`,
      username: this.TURN_USERNAME,
      credential: this.TURN_PASSWORD
    });

    iceServers.push({
      urls: `turns:${turnHost}:${this.TURN_SERVER_TLS_PORT}`,
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

  static getCoturnConfig() {
    const turnHost = this.TURN_SERVER_HOST;
    
    return {
      'listening-port': this.TURN_SERVER_PORT,
      'tls-listening-port': this.TURN_SERVER_TLS_PORT,
      'listening-ip': '0.0.0.0',
      'external-ip': turnHost,
      'relay-ip': turnHost,
      'fingerprint': true,
      'lt-cred-mech': true,
      'user': `${this.TURN_USERNAME}:${this.TURN_PASSWORD}`,
      'realm': this.TURN_REALM,
      'server-name': this.TURN_REALM,
      'total-quota': 100,
      'stale-nonce': true,
      'no-tls': true,
      'no-dtls': true,
      'log-file': '/tmp/turnserver-robot.log',
      'pidfile': '/tmp/turnserver-robot.pid',
      'verbose': true,
      'simple-log': true,
      'new-log-timestamp-format': true,
      'no-cli': true,
      'max-bps': 1000000,
      'min-port': 49152,
      'max-port': 65535,
      'no-multicast-peers': true,
      'mobility': true
    };
  }

  static async checkTurnServerHealth() {
    const net = require('net');
    const turnHost = this.TURN_SERVER_HOST;
    
    return new Promise((resolve) => {
      const socket = new net.Socket();
      const timeout = setTimeout(() => {
        socket.destroy();
        resolve(false);
      }, 5000);

      socket.connect(this.TURN_SERVER_PORT, turnHost, () => {
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

  static logDiagnostics() {
    const turnHost = this.TURN_SERVER_HOST;
    console.log('🔧 TURN конфигурация:');
    console.log(`   Хост: ${turnHost}`);
    console.log(`   Порт UDP/TCP: ${this.TURN_SERVER_PORT}`);
    console.log(`   Порт TLS: ${this.TURN_SERVER_TLS_PORT}`);
    console.log(`   Пользователь: ${this.TURN_USERNAME}`);
    console.log(`   Realm: ${this.TURN_REALM}`);
    console.log(`   Учетные данные: ${this.TURN_PASSWORD ? 'установлены' : 'не установлены'}`);
  }
}

module.exports = TurnConfig; 