# 🚁 Настройка Pixhawk 6C для управления роботом

## 📋 Требования

### Аппаратные требования:
- **Pixhawk 6C** автопилот
- **USB кабель** для подключения к компьютеру
- **Моторы** подключенные к PWM выходам 1-2
- **ESC (регуляторы скорости)** для моторов

### Программные требования:
```bash
# Установка зависимостей
pip install -r requirements-pixhawk.txt
```

## 🔌 Подключение

### 1. Физическое подключение:
```
Pixhawk 6C PWM выходы (4-колесная конфигурация):
├── PWM 1 → Левое переднее колесо (ESC)
├── PWM 2 → Левое заднее колесо (ESC)
├── PWM 3 → Правое переднее колесо (ESC)
├── PWM 4 → Правое заднее колесо (ESC)
├── PWM 5 → Камера Pan (опционально)
└── PWM 6 → Камера Tilt (опционально)

USB подключение:
Pixhawk USB ↔ Raspberry Pi/Компьютер
```

### 2. Настройка портов на Raspberry Pi:
```bash
# Проверка доступных портов
ls /dev/ttyACM* /dev/ttyUSB*

# На Raspberry Pi Pixhawk обычно появляется как:
# /dev/ttyACM0 (наиболее частый случай)
# /dev/ttyUSB0 (с USB-Serial адаптером)

# Автоматический поиск Pixhawk:
python3 detect_pixhawk_port.py
```

## ⚙️ Настройка Pixhawk

### 1. Настройка через QGroundControl:

1. **Подключите Pixhawk к компьютеру**
2. **Откройте QGroundControl**
3. **Выберите тип рамы**: `Rover` или `Generic Ground Vehicle`
4. **Настройте PWM выходы**:
   - PWM 1: Left Front Motor
   - PWM 2: Left Rear Motor  
   - PWM 3: Right Front Motor
   - PWM 4: Right Rear Motor
5. **Установите режим**: `MANUAL`
6. **Калибруйте ESC** (если необходимо)

### 2. Параметры ArduPilot:
```
SERVO1_FUNCTION = 73  # ThrottleLeft (переднее левое)
SERVO2_FUNCTION = 73  # ThrottleLeft (заднее левое)
SERVO3_FUNCTION = 74  # ThrottleRight (переднее правое)
SERVO4_FUNCTION = 74  # ThrottleRight (заднее правое)
SERVO1_MIN = 1000
SERVO1_MAX = 2000
SERVO2_MIN = 1000
SERVO2_MAX = 2000
SERVO3_MIN = 1000
SERVO3_MAX = 2000
SERVO4_MIN = 1000
SERVO4_MAX = 2000
```

## 🚀 Запуск робота

### Поиск Pixhawk:
```bash
# Сначала найдите порт Pixhawk
python3 detect_pixhawk_port.py
```

### Базовый запуск с Pixhawk:
```bash
python3 robot_client.py --use-motors --pixhawk
```

### Расширенные опции:
```bash
# Указать порт и скорость (для Raspberry Pi обычно ttyACM0)
python3 robot_client.py --use-motors --pixhawk \
    --pixhawk-port /dev/ttyACM0 \
    --pixhawk-baud 57600

# С отладкой
python3 robot_client.py --use-motors --pixhawk --debug

# С тестовым видео
python3 robot_client.py --use-motors --pixhawk --test-video
```

### Все параметры:
```bash
python3 robot_client.py \
    --server ws://193.169.240.11:8080 \
    --use-motors \
    --pixhawk \
    --pixhawk-port /dev/ttyACM0 \
    --pixhawk-baud 57600 \
    --debug \
    --test-video
```

## 🔧 Тестирование

### 1. Тест подключения:
```bash
python3 pixhawk_motor_controller.py
```

### 2. Проверка конфигурации:
```bash
python3 pixhawk_config.py
```

### 3. Мониторинг MAVLink:
```bash
# Установка mavproxy (опционально)
pip install mavproxy

# Мониторинг сообщений
mavproxy.py --master=/dev/ttyUSB0 --baudrate=57600
```

## 📊 Диагностика

### Проверка подключения:
```python
from pymavlink import mavutil

# Подключение
connection = mavutil.mavlink_connection('/dev/ttyUSB0', baud=57600)

# Ожидание heartbeat
connection.wait_heartbeat()
print(f"Подключен к системе {connection.target_system}")
```

### Логи робота:
```bash
# Запуск с подробными логами
python3 robot_client.py --use-motors --pixhawk --debug

# Ожидаемые сообщения:
# ✅ Подключен к Pixhawk! System ID: 1
# 🔧 Установка режима: MANUAL
# 🔄 Поток команд запущен
# 📡 PWM отправлен: L=1500μs, R=1500μs
```

## ⚠️ Безопасность

### Важные моменты:
1. **Всегда тестируйте без пропеллеров/колес**
2. **Держите кнопку аварийной остановки под рукой**
3. **Проверьте направление вращения моторов**
4. **Убедитесь в правильности подключения ESC**

### Аварийная остановка:
```bash
# Ctrl+C в терминале робота
# Или отключение USB кабеля
```

## 🔄 Калибровка

### Калибровка ESC:
1. Установите максимальный газ (PWM 2000)
2. Включите ESC
3. Дождитесь звукового сигнала
4. Установите минимальный газ (PWM 1000)
5. Дождитесь подтверждающего сигнала

### Настройка trim:
Отредактируйте `pixhawk_config.py`:
```python
MOTOR_CALIBRATION = {
    'left_motors': {
        'forward_trim': 0.05,   # Если левые моторы слабее
        'reverse_trim': -0.02,
    },
    'right_motors': {
        'forward_trim': -0.03,  # Если правые моторы сильнее
        'reverse_trim': 0.01,
    }
}
```

## 🐛 Устранение неполадок

### Проблема: "Не удалось подключиться к Pixhawk"
**Решение:**
```bash
# 1. Автоматический поиск порта
python3 detect_pixhawk_port.py

# 2. Проверьте порт вручную
ls /dev/ttyACM* /dev/ttyUSB*

# 3. Проверьте права доступа (замените на ваш порт)
sudo chmod 666 /dev/ttyACM0

# 4. Добавьте пользователя в группу dialout
sudo usermod -a -G dialout $USER
# После этого перелогиньтесь или выполните:
newgrp dialout
```

### Проблема: "Моторы не вращаются"
**Решение:**
1. Проверьте режим: должен быть `MANUAL`
2. Проверьте ARM статус
3. Проверьте подключение ESC
4. Проверьте параметры SERVO_FUNCTION

### Проблема: "Неправильное направление"
**Решение:**
```python
# В pixhawk_motor_controller.py поменяйте каналы:
self.left_motor_channels = [3, 4]   # Было [1, 2]
self.right_motor_channels = [1, 2]  # Было [3, 4]
```

## 📚 Дополнительные ресурсы

- [ArduPilot Rover Documentation](https://ardupilot.org/rover/)
- [Pixhawk 6C Manual](https://docs.px4.io/main/en/flight_controller/pixhawk6c.html)
- [MAVLink Protocol](https://mavlink.io/en/)
- [QGroundControl](http://qgroundcontrol.com/) 