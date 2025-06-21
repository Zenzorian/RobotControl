#!/usr/bin/env python3
"""
Конфигурация для Pixhawk 6C контроллера
"""

# Настройки подключения для Raspberry Pi
PIXHAWK_CONNECTIONS = {
    'usb': '/dev/ttyACM0',      # USB подключение на Raspberry Pi (обычно ttyACM0)
    'usb_alt': '/dev/ttyUSB0',  # Альтернативный USB порт
    'uart': '/dev/ttyAMA0',     # UART на Raspberry Pi
    'tcp': 'tcp:127.0.0.1:5760' # TCP соединение (для симуляции)
}

# Скорости соединения
BAUDRATES = {
    'standard': 57600,
    'high': 115200,
    'low': 9600
}

# Настройки PWM каналов (4-колесная конфигурация)
PWM_CHANNELS = {
    'left_motors': [1, 2],   # Каналы 1,2 для левых колес
    'right_motors': [3, 4],  # Каналы 3,4 для правых колес
    'camera_pan': 5,         # Канал 5 для поворота камеры (опционально)
    'camera_tilt': 6,        # Канал 6 для наклона камеры (опционально)
}

# Диапазоны PWM (микросекунды)
PWM_RANGES = {
    'min': 1000,      # Минимальное значение PWM
    'neutral': 1500,  # Нейтральное значение (остановка)
    'max': 2000,      # Максимальное значение PWM
}

# Настройки безопасности
SAFETY_SETTINGS = {
    'max_speed': 0.8,           # Максимальная скорость (80%)
    'acceleration_limit': 0.1,   # Максимальное изменение скорости за шаг
    'heartbeat_timeout': 2.0,    # Таймаут heartbeat (секунды)
    'command_timeout': 1.0,      # Таймаут команд (секунды)
}

# Режимы полета для разных типов роботов
FLIGHT_MODES = {
    'rover': 'MANUAL',      # Для наземных роботов
    'boat': 'MANUAL',       # Для водных роботов
    'copter': 'STABILIZE',  # Для квадрокоптеров
}

# Типы роботов
ROBOT_TYPES = {
    'four_wheel_drive': {
        'description': '4-колесный дифференциальный привод',
        'channels': ['left_motors', 'right_motors'],
        'pwm_channels': [1, 2, 3, 4],
        'mode': 'MANUAL'
    },
    'differential_drive': {
        'description': '2-колесный дифференциальный привод',
        'channels': ['left_motor', 'right_motor'],
        'pwm_channels': [1, 2],
        'mode': 'MANUAL'
    },
    'ackermann': {
        'description': 'Автомобильное управление',
        'channels': ['throttle', 'steering'],
        'pwm_channels': [1, 2],
        'mode': 'MANUAL'
    }
}

# Калибровочные значения для моторов (4-колесная конфигурация)
MOTOR_CALIBRATION = {
    'left_motors': {
        'forward_trim': 0.0,    # Коррекция для движения вперед
        'reverse_trim': 0.0,    # Коррекция для движения назад
        'deadzone': 0.05,       # Мертвая зона
    },
    'right_motors': {
        'forward_trim': 0.0,
        'reverse_trim': 0.0,
        'deadzone': 0.05,
    }
}

# Функции для получения настроек
def get_connection_string(connection_type='usb'):
    """Получить строку подключения"""
    return PIXHAWK_CONNECTIONS.get(connection_type, PIXHAWK_CONNECTIONS['usb'])

def get_baudrate(speed='standard'):
    """Получить скорость соединения"""
    return BAUDRATES.get(speed, BAUDRATES['standard'])

def get_pwm_channel(channel_name):
    """Получить номер PWM канала"""
    return PWM_CHANNELS.get(channel_name, 1)

def get_robot_config(robot_type='differential_drive'):
    """Получить конфигурацию робота"""
    return ROBOT_TYPES.get(robot_type, ROBOT_TYPES['differential_drive'])

def apply_motor_calibration(motor_name, speed):
    """Применить калибровку к скорости мотора"""
    calibration = MOTOR_CALIBRATION.get(motor_name, {})
    
    # Применяем мертвую зону
    deadzone = calibration.get('deadzone', 0.0)
    if abs(speed) < deadzone:
        return 0.0
    
    # Применяем коррекцию
    if speed > 0:
        trim = calibration.get('forward_trim', 0.0)
    else:
        trim = calibration.get('reverse_trim', 0.0)
    
    return speed + trim

# Проверка конфигурации
if __name__ == "__main__":
    print("🔧 Конфигурация Pixhawk 6C")
    print("=" * 40)
    
    print(f"📡 Подключения:")
    for name, conn in PIXHAWK_CONNECTIONS.items():
        print(f"  {name}: {conn}")
    
    print(f"\n⚡ Скорости:")
    for name, baud in BAUDRATES.items():
        print(f"  {name}: {baud}")
    
    print(f"\n🎮 PWM каналы:")
    for name, channel in PWM_CHANNELS.items():
        print(f"  {name}: канал {channel}")
    
    print(f"\n🤖 Типы роботов:")
    for name, config in ROBOT_TYPES.items():
        print(f"  {name}: {config['description']}")
    
    print(f"\n🛡️ Безопасность:")
    for name, value in SAFETY_SETTINGS.items():
        print(f"  {name}: {value}") 