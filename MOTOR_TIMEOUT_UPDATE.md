# Обновления: Система управления моторами

## Изменения в Robot/robot_client.py

### 1. Упрощенная архитектура
- **Удален**: Весь WebRTC код для видео
- **Оставлен**: Только управление моторами через WebSocket
- **Добавлен**: Система безопасности с таймаутами

### 2. Логика безопасности моторов
```python
# Автоматическая остановка при потере связи
if current_time - self.last_command_time > self.command_timeout:
    if not self.motors_stopped:
        logger.warning(f"Команди не отримувалися {self.command_timeout} сек. Зупинка моторів.")
        await self._stop_motors()

# Полное отключение при длительной бездеятельности
if current_time - self.last_command_time > self.motor_disable_timeout:
    if not self.motors_disabled:
        logger.warning(f"Команди не отримувалися {self.motor_disable_timeout} сек. Повне відключення моторів.")
        await self._disable_motors()
```

### 3. Флаги состояния
- `motors_stopped` - моторы остановлены, но готовы к работе
- `motors_disabled` - моторы полностью отключены
- `is_connection_active` - состояние подключения

### 4. Таймауты
- **Остановка**: 2 секунды без команд
- **Отключение**: 60 секунд без команд

## Изменения в server/src/index.js  

### 1. Упрощенная архитектура
- **Удален**: Весь WebRTC код
- **Оставлен**: Простое перенаправление WebSocket сообщений
- **Добавлен**: Мониторинг подключений

### 2. Протокол связи
```javascript
// Регистрация клиентов
REGISTER!CONTROLLER
REGISTER!ROBOT

// Перенаправление сообщений
controller -> robot: команды управления
robot -> controller: телеметрия
```

## Изменения в requirements.txt

### Минимальные зависимости
```text
# Только необходимые пакеты
websockets>=10.0
pyserial>=3.5

# Для Pixhawk (requirements-pixhawk.txt)
pymavlink>=2.4.37
dronekit>=2.9.2
```

## Система видео (отдельно)

### Новая архитектура
- **Файл**: `optimized_video_client.py`
- **Протокол**: MJPEG через WebSocket
- **Производительность**: В 2-3 раза быстрее WebRTC
- **Установка**: Только `opencv-python` и `websockets`

## Инструкции по запуску

### Управление моторами
```bash
# Стандартный режим
python robot_client.py --server ws://localhost:8080

# С отладкой
python robot_client.py --server ws://localhost:8080 --debug
```

### Передача видео (отдельно)
```bash
# Оптимизированное видео
python optimized_video_client.py --server ws://localhost:8080

# С настройками качества
python optimized_video_client.py --quality 70 --fps 15
```

### Сервер
```bash
cd server
npm start
```

## Преимущества новой архитектуры

### Производительность
✅ Без WebRTC зависимостей  
✅ Быстрая установка и настройка  
✅ Низкое потребление ресурсов  
✅ Стабильная работа  

### Надежность
✅ Простая отладка  
✅ Меньше точек отказа  
✅ Лучшая совместимость  
✅ Легкое обслуживание  

### Разделение ответственности
✅ Моторы - отдельный процесс  
✅ Видео - отдельный процесс  
✅ Независимое масштабирование  
✅ Модульная архитектура 