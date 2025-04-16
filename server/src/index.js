const express = require('express');
const https = require('https');
const WebSocket = require('ws');
const fs = require('fs');
const path = require('path');
const os = require('os');
const cors = require('cors');

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

// Настройка CORS
app.use(cors());

// Загрузка SSL-сертификатов
let sslOptions;
const keyPath = path.join(__dirname, '../ssl/private.key');
const certPath = path.join(__dirname, '../ssl/certificate.crt');

console.log('Проверяю пути сертификатов:');
console.log('Private key:', keyPath, 'существует:', fs.existsSync(keyPath));
console.log('Certificate:', certPath, 'существует:', fs.existsSync(certPath));

try {
    sslOptions = {
        key: fs.readFileSync(keyPath),
        cert: fs.readFileSync(certPath),
        // Настройки безопасности
        minVersion: 'TLSv1.2',
        ciphers: 'HIGH:!aNULL:!MD5:!RC4:!3DES',
        honorCipherOrder: true
    };
} catch (error) {
    console.error('Ошибка загрузки SSL-сертификатов:', error);
    process.exit(1);
}

// Создание HTTPS сервера
const server = https.createServer(sslOptions, app);

// Создание WebSocket сервера с дополнительными настройками безопасности
const wss = new WebSocket.Server({ 
    server,
    host: '0.0.0.0', // Принимаем соединения с любого IP
    // Дополнительные настройки безопасности
    verifyClient: (info, callback) => {
        // Здесь можно добавить дополнительную проверку клиентов
        callback(true);
    },
    // Настройки для предотвращения DoS атак
    maxPayload: 1048576, // 1MB
    clientTracking: true
});

// Хранение подключенных клиентов
const clients = new Set();

wss.on('connection', (ws, req) => {
    console.log('Новое подключение с IP:', req.socket.remoteAddress);
    clients.add(ws);

    // Установка таймаута для неактивных соединений
    const timeout = setTimeout(() => {
        if (ws.readyState === WebSocket.OPEN) {
            console.log('Таймаут соединения');
            ws.close();
        }
    }, 30000); // 30 секунд

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
                message: 'Ошибка обработки сообщения'
            }));
        }
    });

    ws.on('close', () => {
        console.log('Клиент отключился');
        clients.delete(ws);
        clearTimeout(timeout);
    });

    ws.on('error', (error) => {
        console.error('Ошибка WebSocket:', error);
        clients.delete(ws);
        clearTimeout(timeout);
    });
});

// Обработка ошибок сервера
server.on('error', (error) => {
    console.error('Ошибка сервера:', error);
});

// Запуск сервера
server.listen(PORT, '0.0.0.0', () => {
    const localIP = getLocalIP();
    console.log(`Сервер запущен на https://${localIP}:${PORT}`);
    console.log(`WebSocket доступен по адресу wss://${localIP}:${PORT}`);
});
