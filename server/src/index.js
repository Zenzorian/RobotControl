const express = require('express');
const WebSocket = require('ws');
const cors = require('cors');
const os = require('os');

const app = express();
app.use(cors());

// Получаем IP адрес сервера
const getLocalIP = () => {
    const interfaces = os.networkInterfaces();
    for (const name of Object.keys(interfaces)) {
        for (const iface of interfaces[name]) {
            if (iface.family === 'IPv4' && !iface.internal) {
                return iface.address;
            }
        }
    }
    return 'localhost';
};

// Создаем HTTP сервер
const server = require('http').createServer(app);

// Создаем WebSocket сервер
const wss = new WebSocket.Server({ 
    server,
    // Разрешаем подключения с любого IP
    host: '0.0.0.0'
});

// Хранилище подключенных клиентов
const clients = new Set();

// Обработка WebSocket соединений
wss.on('connection', (ws) => {
    console.log('Новое подключение');
    clients.add(ws);

    // Обработка сообщений от клиента
    ws.on('message', (message) => {
        try {
            const data = JSON.parse(message);
            console.log('Получено сообщение:', data);

            // Здесь будет логика обработки команд для робота
            // Например, отправка команд на Arduino через Serial порт

            // Отправляем подтверждение клиенту
            ws.send(JSON.stringify({
                type: 'status',
                message: 'Команда получена'
            }));
        } catch (error) {
            console.error('Ошибка обработки сообщения:', error);
            ws.send(JSON.stringify({
                type: 'error',
                message: 'Ошибка обработки команды'
            }));
        }
    });

    // Обработка отключения клиента
    ws.on('close', () => {
        console.log('Клиент отключился');
        clients.delete(ws);
    });
});

// Стартуем сервер
const PORT = process.env.PORT || 8080;
const HOST = process.env.HOST || '0.0.0.0';
server.listen(PORT, HOST, () => {
    const localIP = getLocalIP();
    console.log(`Сервер запущен на порту ${PORT}`);
    console.log(`Доступен по адресу: ws://${localIP}:${PORT}`);
}); 