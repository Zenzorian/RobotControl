# WebRTC Сигналинг Сервер для Робототехники

Современный WebSocket сервер с поддержкой WebRTC сигналинга для управления роботами в реальном времени. Архитектура основана на принципах SOLID.

Развернут на: **193.169.240.11:8080**

## 🎯 Основные функции

- **WebRTC Сигналинг** - низколатентная передача видео
- **Командное управление** - WebSocket команды в реальном времени
- **Масштабируемость** - поддержка множественных роботов
- **Мониторинг** - детальная статистика WebRTC сессий
- **SOLID архитектура** - легкое расширение и тестирование

## 🏗️ Архитектура

```
Robot ←→ WebRTC P2P Video ←→ Unity Client
   ↓                               ↓
   ↓ ←→ WebSocket Signaling Server ←→
       (193.169.240.11:8080)
```

### Компоненты:

- **ServerManager** - управление жизненным циклом
- **WebRTCSignalingService** - обработка WebRTC сигналов
- **ClientManagerService** - управление WebSocket клиентами
- **MessageHandler** - обработка сообщений
- **ApiRoutes** - REST API эндпоинты

## 🚀 Быстрый старт

### 1. Установка

```bash
git clone <your-repo>
cd server
npm install
```

### 2. Запуск

```bash
npm start
```

### 3. Проверка

Откройте в браузере: `http://193.169.240.11:8080/api/status`

## ⚙️ Конфигурация

### Переменные окружения

```bash
PORT=8080                    # Порт сервера
NODE_ENV=production         # Окружение
LOG_LEVEL=info              # Уровень логирования
```

### WebRTC конфигурация

Автоматически создается при первом запуске:
`src/config/webrtc-config.json`

## 📡 API Эндпоинты

### Основные

- `GET /api/status` - статус сервера
- `GET /api/health` - health check
- `GET /api/connections` - активные подключения

### WebRTC

- `GET /api/webrtc/stats` - статистика WebRTC
- `GET /api/webrtc/sessions` - активные сессии
- `GET /api/webrtc/config` - конфигурация для клиентов

### Детальная статистика

```bash
curl http://193.169.240.11:8080/api/status/detailed
```

## 🔧 Использование

### Подключение робота

```python
# Python (robot side)
import websocket

ws = websocket.WebSocket()
ws.connect("ws://193.169.240.11:8080")
ws.send("REGISTER!ROBOT")
```

### Подключение контроллера

```csharp
// C# Unity (controller side)
var ws = new WebSocket("ws://193.169.240.11:8080");
ws.Send("REGISTER!CONTROLLER");
```

### WebRTC сигналинг

```javascript
// Отправка WebRTC offer
const offerMessage = {
  type: "webrtc-signal",
  signalType: "offer",
  sessionId: "unique-session-id",
  data: {
    sdp: offer.sdp,
    type: offer.type
  }
};
ws.send(JSON.stringify(offerMessage));
```

## 📊 Мониторинг

### WebRTC статистика

```bash
curl http://193.169.240.11:8080/api/webrtc/stats
```

**Ответ:**
```json
{
  "activeSessions": 1,
  "totalSessions": 5,
  "stats": {
    "sessionsCreated": 5,
    "sessionsCompleted": 4,
    "signalsProcessed": 23
  }
}
```

### Активные сессии

```bash
curl http://193.169.240.11:8080/api/webrtc/sessions
```

## 🧪 Тестирование

```bash
npm test
```

### Unit тесты

- ServerManager инициализация
- WebRTC сигналинг
- Управление клиентами
- API эндпоинты

## 📝 Логирование

### Типичные логи

```
🚀 WebRTC Сигналинг Сервер запущен на порту 8080
🤖 Клиент робота зарегистрирован
🎮 Клиент управления зарегистрирован
📡 WebRTC сигнал: offer от robot
✅ WebRTC offer переслан контроллеру
🎉 WebRTC сессия установлена
```

### Уровни логирования

- **INFO** - основные события
- **DEBUG** - детальная информация
- **ERROR** - ошибки и исключения

## 🔧 Диагностика

### Проблемы WebRTC

1. **Проверьте логи сервера** - должны быть WebRTC сигналы
2. **Проверьте статистику** - `/api/webrtc/stats`
3. **Проверьте firewall** - порт 8080 должен быть открыт

### Проблемы подключения

1. **Health check** - `curl http://193.169.240.11:8080/api/health`
2. **Статус подключений** - `/api/connections`
3. **WebSocket лог** - проверьте регистрацию клиентов

### Сетевая диагностика

```bash
# Проверка доступности сервера
ping 193.169.240.11

# Проверка порта WebSocket
telnet 193.169.240.11 8080

# Тест WebSocket подключения
wscat -c ws://193.169.240.11:8080
```

## 🌍 Развертывание

### Production

```bash
NODE_ENV=production npm start
```

### Docker

```dockerfile
FROM node:18-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY . .
EXPOSE 8080
CMD ["npm", "start"]
```

### systemd service

```ini
[Unit]
Description=WebRTC Signaling Server
After=network.target

[Service]
Type=simple
User=robotuser
WorkingDirectory=/path/to/server
ExecStart=/usr/bin/node src/index.js
Restart=always
Environment=NODE_ENV=production
Environment=PORT=8080

[Install]
WantedBy=multi-user.target
```

## 🔐 Безопасность

- Валидация всех входящих сообщений
- Rate limiting для WebSocket подключений
- Очистка старых WebRTC сессий
- Graceful shutdown

## 📚 Документация

- [Архитектура](ARCHITECTURE.md) - детальная архитектура
- [WebRTC Setup](WEBRTC_SETUP.md) - настройка WebRTC
- [Migration Guide](MIGRATION_GUIDE.md) - руководство по миграции

## 🤝 Вклад в проект

1. Fork репозитория
2. Создайте feature branch
3. Добавьте тесты
4. Создайте pull request

## 📄 Лицензия

MIT License 