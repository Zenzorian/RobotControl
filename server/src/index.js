const express = require('express');
const https = require('https');
const WebSocket = require('ws');
const fs = require('fs');
const path = require('path');
const os = require('os');

// Получение локального IP-адреса
function getLocalIP() {
    const interfaces = os.networkInterfaces();
    for (const name of Object.keys(interfaces)) {
        for (const iface of interfaces[name]) {
            if (iface.family === 'IPv4' && !iface.internal) {
                return iface.address;
            }
        }
    }
    return 'localhost';
}

const app = express();
const PORT = process.env.PORT || 8080;

// Загрузка SSL-сертификатов
const sslOptions = {
    key: fs.readFileSync('/home/ubuntu/RobotControl/server/ssl/private.key'),
    cert: fs.readFileSync('/home/ubuntu/RobotControl/server/ssl/certificate.crt')
};

// Создание HTTPS сервера
const server = https.createServer(sslOptions, app);

// Создание WebSocket сервера
const wss = new WebSocket.Server({ 
    server,
    host: '0.0.0.0' // Принимаем соединения с любого IP
});

// Хранение подключенных клиентов
const clients = new Set();

wss.on('connection', (ws) => {
    console.log('Новое подключение');
    clients.add(ws);

    ws.on('message', (message) => {
        try {
            const data = JSON.parse(message);
            console.log('Получено сообщение:', data);

            // Обработка сообщений
            if (data.type === 'command') {
                // Здесь будет логика обработки команд
                ws.send(JSON.stringify({
                    type: 'status',
                    data: {
                        status: 'success',
                        message: 'Команда получена'
                    }
                }));
            }
        } catch (error) {
            console.error('Ошибка обработки сообщения:', error);
            ws.send(JSON.stringify({
                type: 'error',
                data: {
                    message: 'Ошибка обработки сообщения'
                }
            }));
        }
    });

    ws.on('close', () => {
        console.log('Клиент отключился');
        clients.delete(ws);
    });
});

// Запуск сервера
server.listen(PORT, '0.0.0.0', () => {
    const localIP = getLocalIP();
    console.log(`Сервер запущен на https://${localIP}:${PORT}`);
    console.log(`WebSocket доступен по адресу wss://${localIP}:${PORT}`);
});
