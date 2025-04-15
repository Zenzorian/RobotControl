# Сервер управления роботом

Серверная часть для управления роботом через WebSocket соединение.

## Требования

- Node.js 14 или новее
- npm или yarn
- Linux сервер с доступом по SSH
- OpenSSL (для генерации SSL-сертификатов)

## Установка на Linux сервер

1. Подключитесь к серверу по SSH:
```bash
ssh username@your-server-ip
```

2. Установите Node.js и npm:

sudo apt install nodejs
sudo apt install npm

```

3. Клонируйте репозиторий:
```bash
git clone https://github.com/Zenzorian/RobotControl.git
cd RobotControl/server
```

4. Установите зависимости:
```bash
npm install
```

5. Установите PM2 глобально:
```bash
sudo npm install -g pm2
```

6. Сгенерируйте SSL-сертификаты:
```bash
chmod +x scripts/generate-ssl.sh
./scripts/generate-ssl.sh
```

7. Запустите сервер в production режиме:
```bash
npm run prod
```

8. Настройте автозапуск PM2:
```bash
pm2 startup
pm2 save
```

## Управление сервером

- Просмотр логов:
```bash
pm2 logs robot-control
```

- Перезапуск сервера:
```bash
pm2 restart robot-control
```

- Остановка сервера:
```bash
pm2 stop robot-control
```

## Настройка брандмауэра

```bash
sudo ufw allow 8080
```

## API

### WebSocket соединение

- URL: `wss://your-server-ip:8080`
- Протокол: WebSocket Secure (WSS)

### Формат сообщений

#### От клиента:
```json
{
    "type": "movement",
    "leftWheels": 100,
    "rightWheels": 100,
    "speed": 50
}
```

#### От сервера:
```json
{
    "type": "status",
    "message": "Команда получена"
}
```

## Мониторинг

- PM2 Dashboard: `pm2 monit`
- Статус процессов: `pm2 status`
- Логи: `pm2 logs robot-control`

## Настройка

1. Измените порт в `src/index.js` если нужно
2. Настройте CORS если требуется
3. Добавьте логику для работы с роботом в обработчик сообщений

### Порт
По умолчанию сервер использует порт 8080. Для изменения порта:
1. Отредактируйте переменную `PORT` в файле `src/index.js`
2. Убедитесь, что порт открыт в брандмауэре

### SSL-сертификаты
Для использования в продакшене рекомендуется использовать сертификаты от доверенного центра сертификации (например, Let's Encrypt). Для тестирования можно использовать самоподписанные сертификаты, сгенерированные скриптом `generate-ssl.sh`.

### Безопасность
- Все соединения используют SSL/TLS
- Приватный ключ имеет ограниченные права доступа (600)
- Сертификат имеет права доступа только для чтения (644)

## Структура проекта
```
server/
├── src/
│   └── index.js      # Основной файл сервера
├── ssl/              # Директория с SSL-сертификатами
│   ├── private.key   # Приватный ключ
│   └── certificate.crt # SSL-сертификат
├── scripts/
│   └── generate-ssl.sh # Скрипт генерации SSL-сертификатов
└── package.json      # Зависимости и скрипты
``` 