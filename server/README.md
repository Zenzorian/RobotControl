# Сервер управления роботом

Серверная часть для управления роботом через WebSocket соединение.

## Требования

- Node.js 14 или новее
- npm или yarn
- Linux сервер с доступом по SSH

## Установка на Linux сервер

1. Подключитесь к серверу по SSH:
```bash
ssh username@your-server-ip
```

2. Установите Node.js и npm:
```bash
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt-get install -y nodejs
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

6. Запустите сервер в production режиме:
```bash
npm run prod
```

7. Настройте автозапуск PM2:
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

- URL: `ws://your-server-ip:8080`
- Протокол: WebSocket

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