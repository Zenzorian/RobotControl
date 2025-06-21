const ServerManager = require('./ServerManager');

// Создание и запуск сервера
const serverManager = new ServerManager();

serverManager.start()
  .then(() => {
    console.log('✅ Сервер успешно запущен');
  })
  .catch((error) => {
    console.error('❌ Ошибка запуска сервера:', error);
    process.exit(1);
  });

// Экспорт для тестирования
module.exports = serverManager; 