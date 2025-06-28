# WebRTC Setup Guide

Полное руководство по настройке WebRTC сигналинга для робототехнической системы.

## 🎯 Обзор архитектуры

```
Robot (Python) ←→ P2P WebRTC Video ←→ Unity Client (C#)
       ↓                                        ↓
       ↓ ←→ WebSocket Signaling Server ←→ ←→ ←→ ↓
           (Node.js на 193.169.240.11:8080)
```

## 🔧 Установка сервера

### 1. Настройка Node.js сервера

```bash
cd server
npm install
NODE_ENV=production npm start
```

Сервер запустится на `193.169.240.11:8080`

### 2. Проверка работы

```bash
curl http://193.169.240.11:8080/api/health
```

**Ожидаемый ответ:**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-01T00:00:00.000Z",
  "uptime": 123.45,
  "services": {
    "websocket": true,
    "webrtc": true
  }
}
```

## 🤖 Настройка робота

### 1. Конфигурация

Файл `Robot/config.json` уже настроен для вашего сервера:

```json
{
  "server": {
    "host": "193.169.240.11",
    "port": 8080,
    "websocket_url": "ws://193.169.240.11:8080"
  },
  "webrtc": {
    "enabled": true,
    "ice_servers": [
      {
        "urls": "stun:stun.l.google.com:19302"
      }
    ]
  }
}
```

### 2. Запуск робота

```bash
cd Robot
python main.py
```

**Ожидаемые логи:**
```
🚀 Запуск робота...
📷 Камера инициализирована: 640x480@30fps
🌐 Подключение к серверу: ws://193.169.240.11:8080
✅ WebSocket подключен
🎥 WebRTC стример готов
🤖 Робот готов к работе!
```

## 🎮 Настройка Unity клиента

### 1. Обновление ServerAddressField

В Unity найдите компонент `ServerAddressField` и установите:
- **Server Address**: `193.169.240.11:8080`
- **Use WebRTC**: ✅ включено

### 2. Код подключения

```csharp
public class RobotController : MonoBehaviour
{
    private WebSocket webSocket;
    private string serverUrl = "ws://193.169.240.11:8080";
    
    async void Start()
    {
        await ConnectToServer();
    }
    
    async Task ConnectToServer()
    {
        webSocket = new WebSocket(serverUrl);
        
        webSocket.OnOpen += () => {
            Debug.Log("✅ Подключен к серверу сигналинга");
            webSocket.Send("REGISTER!CONTROLLER");
        };
        
        webSocket.OnMessage += HandleMessage;
        await webSocket.Connect();
    }
}
```

## 📡 WebRTC сигналинг

### 1. Процесс установки соединения

```mermaid
sequenceDiagram
    participant R as Robot
    participant S as Signaling Server
    participant U as Unity Client
    
    R->>S: REGISTER!ROBOT
    U->>S: REGISTER!CONTROLLER
    
    R->>S: WebRTC Offer
    S->>U: Переслать Offer
    
    U->>S: WebRTC Answer
    S->>R: Переслать Answer
    
    R->>S: ICE Candidate
    S->>U: Переслать ICE
    
    U->>S: ICE Candidate
    S->>R: Переслать ICE
    
    R<-->U: P2P Video Stream
```

### 2. Пример WebRTC сигналов

**Отправка Offer (Robot → Unity):**
```python
offer_message = {
    "type": "webrtc-signal",
    "signalType": "offer",
    "sessionId": str(uuid.uuid4()),
    "data": {
        "sdp": offer.sdp,
        "type": offer.type
    }
}
ws.send(json.dumps(offer_message))
```

**Отправка Answer (Unity → Robot):**
```csharp
var answerMessage = new {
    type = "webrtc-signal",
    signalType = "answer",
    sessionId = sessionId,
    data = new {
        sdp = answer.sdp,
        type = answer.type
    }
};
webSocket.Send(JsonUtility.ToJson(answerMessage));
```

## 📊 Мониторинг

### 1. WebRTC статистика

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
    "signalsProcessed": 23,
    "averageSetupTime": 1.2
  }
}
```

### 2. Активные сессии

```bash
curl http://193.169.240.11:8080/api/webrtc/sessions
```

### 3. Общий статус

```bash
curl http://193.169.240.11:8080/api/status/detailed
```

## 🔧 Диагностика

### 1. Проверка сетевого подключения

```bash
# Проверка доступности сервера
ping 193.169.240.11

# Проверка порта
telnet 193.169.240.11 8080

# Проверка WebSocket
wscat -c ws://193.169.240.11:8080
```

### 2. Проверка WebRTC сигналинга

```bash
# Отправка тестового сигнала
curl -X POST http://193.169.240.11:8080/api/webrtc/test \
  -H "Content-Type: application/json" \
  -d '{"type": "test"}'
```

### 3. Логи сервера

```bash
# На сервере
cd server
npm start | grep -E "(WebRTC|ERROR|WARN)"
```

**Типичные логи:**
```
🚀 WebRTC Сигналинг Сервер запущен на порту 8080
🤖 Клиент робота зарегистрирован
🎮 Клиент управления зарегистрирован
📡 WebRTC сигнал: offer от robot
✅ WebRTC offer переслан контроллеру
📡 WebRTC сигнал: answer от controller
✅ WebRTC answer переслан роботу
🎉 WebRTC сессия установлена (ID: abc123)
```

## 🚨 Устранение неисправностей

### 1. Робот не подключается

**Проблема:** `Connection refused`
**Решение:**
```bash
# Проверьте что сервер запущен
curl http://193.169.240.11:8080/api/health

# Проверьте firewall
sudo ufw status
sudo ufw allow 8080
```

### 2. WebRTC не работает

**Проблема:** Видео не передается
**Решение:**
1. Проверьте ICE серверы в конфигурации
2. Убедитесь что оба клиента получают сигналы
3. Проверьте NAT/firewall настройки

### 3. Высокая задержка

**Проблема:** Задержка > 500ms
**Решение:**
1. Используйте ближайшие STUN серверы
2. Настройте TURN сервер для enterprise сетей
3. Оптимизируйте bitrate в конфигурации

## 🌐 Использование через интернет

### 1. Настройка STUN/TURN

Для работы через интернет добавьте в `Robot/config.json`:

```json
{
  "webrtc": {
    "ice_servers": [
      {
        "urls": "stun:stun.l.google.com:19302"
      },
      {
        "urls": "turn:your-turn-server.com:3478",
        "username": "user",
        "credential": "pass"
      }
    ]
  }
}
```

### 2. Проверка подключения

```bash
# Проверка STUN сервера
npm run check-webrtc
```

## ✅ Контрольный список настройки

- [ ] Сервер запущен на `193.169.240.11:8080`
- [ ] Health check отвечает успешно
- [ ] Робот подключается к серверу
- [ ] Unity клиент подключается к серверу
- [ ] WebRTC сигналы передаются
- [ ] P2P видео поток работает
- [ ] Задержка < 200ms
- [ ] Команды управления работают

## 📞 Поддержка

При проблемах проверьте:
1. Логи сервера: `/api/status/detailed`
2. WebRTC статистику: `/api/webrtc/stats`
3. Network connectivity: `ping 193.169.240.11` 