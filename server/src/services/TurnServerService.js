const { spawn } = require('child_process');
const fs = require('fs').promises;
const path = require('path');
const TurnConfig = require('../config/TurnConfig');

/**
 * TURN Server Service
 * Отвечает за управление TURN-сервером (coturn)
 * Интегрируется с существующей архитектурой WebRTC сигналинга
 */
class TurnServerService {
  constructor() {
    this.turnProcess = null;
    this.isRunning = false;
    this.configFile = '/tmp/turnserver-robot.conf';
    this.logFile = '/tmp/turnserver-robot.log';
    this.pidFile = '/tmp/turnserver-robot.pid';
    this.stats = {
      startedAt: null,
      restarts: 0,
      connections: 0,
      totalBytes: 0
    };

    console.log('🔄 TURN Server Service инициализирован');
  }

  /**
   * Запуск TURN-сервера
   */
  async start() {
    try {
      console.log('🚀 Запуск TURN-сервера...');

      // Проверяем, установлен ли coturn
      if (!(await this.checkCoturnInstalled())) {
        console.log('⚠️ coturn не установлен. Попробуем установить...');
        try {
          await this.installCoturn();
        } catch (error) {
          console.log('❌ Не удалось установить coturn, но продолжаем (возможно уже установлен системно)');
        }
      }

      // Останавливаем существующие TURN процессы
      await this.killExistingTurnServers();
      await new Promise(resolve => setTimeout(resolve, 2000)); // Ждем 2 секунды

      // Проверяем доступность портов
      const portsAvailable = await this.checkPortAvailability();
      if (!portsAvailable) {
        console.log('⚠️ Некоторые порты заняты, но попробуем запустить...');
      }

      // Создаем конфигурационный файл
      await this.createConfigFile();

      // Запускаем TURN-сервер
      await this.startTurnServer();

      // Проверяем здоровье сервера
      const isHealthy = await this.healthCheck();
      if (isHealthy) {
        console.log('✅ TURN-сервер запущен и работает');
        this.stats.startedAt = new Date();
        return true;
      } else {
        console.log('❌ TURN-сервер не прошел проверку здоровья');
        return false;
      }
    } catch (error) {
      console.error('💥 Ошибка запуска TURN-сервера:', error);
      return false;
    }
  }

  /**
   * Остановка TURN-сервера
   */
  async stop() {
    try {
      console.log('🛑 Остановка TURN-сервера...');

      if (this.turnProcess) {
        this.turnProcess.kill('SIGTERM');
        this.turnProcess = null;
      }

      this.isRunning = false;
      console.log('✅ TURN-сервер остановлен');
    } catch (error) {
      console.error('💥 Ошибка остановки TURN-сервера:', error);
    }
  }

  /**
   * Проверка установки coturn
   */
  async checkCoturnInstalled() {
    try {
      const { exec } = require('child_process');
      return new Promise((resolve) => {
        exec('which turnserver', (error) => {
          resolve(!error);
        });
      });
    } catch (error) {
      return false;
    }
  }

  /**
   * Установка coturn
   */
  async installCoturn() {
    console.log('📦 Установка coturn...');
    
    return new Promise((resolve, reject) => {
      const install = spawn('apt-get', ['update', '&&', 'apt-get', 'install', '-y', 'coturn'], {
        stdio: 'inherit',
        shell: true
      });

      install.on('close', (code) => {
        if (code === 0) {
          console.log('✅ coturn установлен успешно');
          resolve();
        } else {
          reject(new Error(`Ошибка установки coturn, код: ${code}`));
        }
      });
    });
  }

  /**
   * Проверка доступности портов
   */
  async checkPortAvailability() {
    const net = require('net');
    
    const checkPort = (port) => {
      return new Promise((resolve) => {
        const server = net.createServer();
        server.listen(port, (err) => {
          if (err) {
            resolve(false);
          } else {
            server.once('close', () => resolve(true));
            server.close();
          }
        });
        server.on('error', () => resolve(false));
      });
    };

    const mainPortAvailable = await checkPort(TurnConfig.TURN_SERVER_PORT);
    const tlsPortAvailable = await checkPort(TurnConfig.TURN_SERVER_TLS_PORT);
    
    console.log(`🔍 Порт ${TurnConfig.TURN_SERVER_PORT}: ${mainPortAvailable ? 'свободен' : 'занят'}`);
    console.log(`🔍 Порт ${TurnConfig.TURN_SERVER_TLS_PORT}: ${tlsPortAvailable ? 'свободен' : 'занят'}`);
    
    return mainPortAvailable && tlsPortAvailable;
  }

  /**
   * Поиск и остановка существующих TURN процессов
   */
  async killExistingTurnServers() {
    try {
      const { exec } = require('child_process');
      return new Promise((resolve) => {
        exec('pkill -f turnserver', (error) => {
          if (error) {
            console.log('ℹ️ Нет запущенных turnserver процессов (или нет прав)');
          } else {
            console.log('🛑 Остановлены существующие turnserver процессы');
          }
          resolve();
        });
      });
    } catch (error) {
      console.log('⚠️ Не удалось остановить существующие процессы:', error.message);
    }
  }

  /**
   * Создание конфигурационного файла
   */
  async createConfigFile() {
    const config = TurnConfig.getCoturnConfig();
    const configLines = [];

    // Преобразуем конфигурацию в формат coturn
    for (const [key, value] of Object.entries(config)) {
      if (typeof value === 'boolean') {
        if (value) configLines.push(key);
      } else {
        configLines.push(`${key}=${value}`);
      }
    }

    // Добавляем дополнительные настройки для Starlink
    configLines.push('');
    configLines.push('# Optimizations for Starlink/Satellite connections');
    configLines.push('max-bps=1000000');
    configLines.push('min-port=49152');
    configLines.push('max-port=65535');
    configLines.push('no-multicast-peers');
    configLines.push('mobility');
    configLines.push('');
    configLines.push('# User-space configuration');
    configLines.push('no-cli');
    configLines.push('no-web-admin');
    configLines.push('');

    const configContent = configLines.join('\n');
    
    try {
      await fs.writeFile(this.configFile, configContent);
      console.log(`📝 Конфигурационный файл создан: ${this.configFile}`);
      console.log(`📄 Содержимое конфигурации:\n${configContent}`);
    } catch (error) {
      console.error('❌ Ошибка создания конфигурационного файла:', error);
      throw error;
    }
  }

  /**
   * Запуск процесса TURN-сервера
   */
  async startTurnServer() {
    return new Promise((resolve, reject) => {
      console.log('▶️ Запуск процесса turnserver...');
      
      this.turnProcess = spawn('turnserver', ['-c', this.configFile], {
        stdio: ['ignore', 'pipe', 'pipe']
      });

      // Логирование вывода
      this.turnProcess.stdout.on('data', (data) => {
        console.log(`[TURN] ${data.toString().trim()}`);
      });

      this.turnProcess.stderr.on('data', (data) => {
        console.log(`[TURN ERROR] ${data.toString().trim()}`);
      });

      // Обработка завершения
      this.turnProcess.on('close', (code) => {
        console.log(`🔄 TURN-сервер завершился с кодом: ${code}`);
        this.isRunning = false;
        
        if (code !== 0) {
          this.stats.restarts++;
          // Автоматический перезапуск через 5 секунд
          setTimeout(() => {
            console.log('🔄 Автоматический перезапуск TURN-сервера...');
            this.start();
          }, 5000);
        }
      });

      this.turnProcess.on('error', (error) => {
        console.error('💥 Ошибка процесса TURN-сервера:', error);
        reject(error);
      });

      // Даем время на запуск
      setTimeout(() => {
        if (this.turnProcess) {
          this.isRunning = true;
          resolve();
        } else {
          reject(new Error('TURN-сервер не запустился'));
        }
      }, 3000);
    });
  }

  /**
   * Проверка здоровья TURN-сервера
   */
  async healthCheck() {
    try {
      const isPortOpen = await TurnConfig.checkTurnServerHealth();
      if (isPortOpen) {
        console.log('✅ TURN-сервер отвечает на порту');
        return true;
      } else {
        console.log('❌ TURN-сервер не отвечает на порту');
        return false;
      }
    } catch (error) {
      console.error('💥 Ошибка проверки здоровья TURN-сервера:', error);
      return false;
    }
  }

  /**
   * Получение статистики TURN-сервера
   */
  getStats() {
    return {
      isRunning: this.isRunning,
      startedAt: this.stats.startedAt,
      restarts: this.stats.restarts,
      connections: this.stats.connections,
      totalBytes: this.stats.totalBytes,
      uptime: this.stats.startedAt ? Date.now() - this.stats.startedAt.getTime() : 0
    };
  }

  /**
   * Получение ICE конфигурации для клиентов
   */
  getICEConfiguration() {
    return TurnConfig.getICEConfiguration();
  }

  /**
   * Получение URL TURN-сервера для WebRTC
   */
  getTurnServerUrls() {
    const host = TurnConfig.TURN_SERVER_HOST;
    const port = TurnConfig.TURN_SERVER_PORT;
    const tlsPort = TurnConfig.TURN_SERVER_TLS_PORT;
    const username = TurnConfig.TURN_USERNAME;
    const credential = TurnConfig.TURN_PASSWORD;

    return [
      {
        urls: `turn:${host}:${port}`,
        username,
        credential
      },
      {
        urls: `turn:${host}:${port}?transport=tcp`,
        username,
        credential
      },
      {
        urls: `turns:${host}:${tlsPort}`,
        username,
        credential
      }
    ];
  }

  /**
   * Мониторинг подключений
   */
  startMonitoring() {
    setInterval(async () => {
      if (this.isRunning) {
        const isHealthy = await this.healthCheck();
        if (!isHealthy) {
          console.log('⚠️ TURN-сервер недоступен, попытка перезапуска...');
          await this.restart();
        }
      }
    }, 30000); // Проверка каждые 30 секунд
  }

  /**
   * Перезапуск TURN-сервера
   */
  async restart() {
    console.log('🔄 Перезапуск TURN-сервера...');
    await this.stop();
    await new Promise(resolve => setTimeout(resolve, 2000));
    return await this.start();
  }
}

module.exports = TurnServerService; 