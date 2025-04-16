const WebSocket = require('ws');
const http = require('http');
const express = require('express');
const path = require('path');

const app = express();
const server = http.createServer(app);
const wss = new WebSocket.Server({ server });

// Статические файлы (если нужно)
app.use(express.static(path.join(__dirname, 'public')));

// Хранение подключенных клиентов
const clients = {
  controller: null, // Клиент управления
  robot: null       // Клиент робота
};

// Обработчик подключения WebSocket
wss.on('connection', (ws) => {
  console.log('Новое подключение установлено');

  // Ожидаем первое сообщение для определения типа клиента
  ws.once('message', (message) => {
    const msgStr = message.toString();
    
    if (msgStr === 'REGISTER!CONTROLLER') {
      clients.controller = ws;
      ws.clientType = 'controller';
      console.log('Клиент управления зарегистрирован');
      ws.send('REGISTERED!CONTROLLER');
    } 
    else if (msgStr === 'REGISTER!ROBOT') {
      clients.robot = ws;
      ws.clientType = 'robot';
      console.log('Клиент робота зарегистрирован');
      ws.send('REGISTERED!ROBOT');
    }
    else {
      console.log('Неизвестный тип клиента, отключение');
      ws.close();
      return;
    }

    // Настройка обработчика сообщений после регистрации
    setupMessageHandler(ws);
  });

  // При отключении клиента
  ws.on('close', () => {
    if (ws.clientType === 'controller') {
      console.log('Клиент управления отключился');
      clients.controller = null;
    } 
    else if (ws.clientType === 'robot') {
      console.log('Клиент робота отключился');
      clients.robot = null;
    }
  });
});

// Обработчик сообщений между клиентами
function setupMessageHandler(ws) {
  ws.on('message', (message) => {
    const msgStr = message.toString();
    const parts = msgStr.split('!');
    const messageType = parts[0];
    
    console.log(`Получено сообщение типа ${messageType} от ${ws.clientType}`);

    // Определяем адресата сообщения
    let targetClient = null;
    if (ws.clientType === 'controller') {
      targetClient = clients.robot;
    } else if (ws.clientType === 'robot') {
      targetClient = clients.controller;
    }

    // Если адресат существует, пересылаем сообщение
    if (targetClient && targetClient.readyState === WebSocket.OPEN) {
      // Обработка сигнальных сообщений WebRTC
      switch (messageType) {
        case 'OFFER':
        case 'ANSWER':
        case 'CANDIDATE':
          // Передаем сигнальное сообщение без изменений
          targetClient.send(msgStr);
          break;
          
        case 'COMMAND':
          // Команды управления от контроллера к роботу
          if (ws.clientType === 'controller') {
            targetClient.send(msgStr);
          }
          break;
          
        case 'TELEMETRY':
          // Данные телеметрии от робота к контроллеру
          if (ws.clientType === 'robot') {
            targetClient.send(msgStr);
          }
          break;
          
        default:
          // Другие сообщения просто пересылаем
          targetClient.send(msgStr);
          break;
      }
    } else {
      console.log(`Целевой клиент (${ws.clientType === 'controller' ? 'робот' : 'контроллер'}) не подключен`);
      ws.send(`ERROR!TARGET_DISCONNECTED!${ws.clientType === 'controller' ? 'ROBOT' : 'CONTROLLER'}`);
    }
  });
}

// Обработка ошибок WebSocket сервера
wss.on('error', (error) => {
  console.error('Ошибка WebSocket сервера:', error);
});

// Запуск сервера
const PORT = process.env.PORT || 8080;
server.listen(PORT, () => {
  console.log(`Сервер запущен на порту ${PORT}`);
});

// Функция для периодической проверки соединений
setInterval(() => {
  wss.clients.forEach((client) => {
    if (client.readyState === WebSocket.OPEN) {
      client.ping();
    }
  });
}, 30000); // Пинг каждые 30 секунд 