#!/usr/bin/env python3
"""
Контроллер моторов для Pixhawk 6C через MAVLink
Отправляет команды управления напрямую в автопилот
"""

import time
import logging
from pymavlink import mavutil
import threading

# Настройка логирования
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("pixhawk_controller")

class PixhawkMotorController:
    def __init__(self, connection_string="/dev/ttyACM0", baudrate=57600):
        """
        Инициализация контроллера Pixhawk
        
        :param connection_string: Порт подключения (USB/UART)
        :param baudrate: Скорость соединения
        """
        self.connection_string = connection_string
        self.baudrate = baudrate
        self.connection = None
        self.is_connected = False
        self.armed = False
        
        # Каналы PWM для моторов (4-колесная конфигурация)
        self.left_motor_channels = [1, 2]   # PWM каналы 1,2 для левых колес
        self.right_motor_channels = [3, 4]  # PWM каналы 3,4 для правых колес
        
        # Диапазон PWM значений (микросекунды)
        self.pwm_min = 1000  # Минимальное значение PWM
        self.pwm_max = 2000  # Максимальное значение PWM
        self.pwm_neutral = 1500  # Нейтральное значение (остановка)
        
        # Текущие значения моторов
        self.current_left_speed = 0.0
        self.current_right_speed = 0.0
        
        # Поток для отправки команд
        self.command_thread = None
        self.running = False
        
        self.connect()
    
    def connect(self):
        """Подключение к Pixhawk"""
        try:
            logger.info(f"🔗 Подключение к Pixhawk: {self.connection_string}:{self.baudrate}")
            
            # Создаем соединение MAVLink
            self.connection = mavutil.mavlink_connection(
                self.connection_string, 
                baud=self.baudrate,
                source_system=255,  # ID нашей наземной станции
                source_component=0
            )
            
            # Ждем первое сообщение heartbeat
            logger.info("⏳ Ожидание heartbeat от Pixhawk...")
            self.connection.wait_heartbeat(timeout=10)
            
            logger.info(f"✅ Подключен к Pixhawk! System ID: {self.connection.target_system}")
            self.is_connected = True
            
            # Запускаем поток для периодической отправки команд
            self.start_command_thread()
            
            # Настраиваем режим MANUAL для прямого управления PWM
            self.set_mode("MANUAL")
            
        except Exception as e:
            logger.error(f"💥 Ошибка подключения к Pixhawk: {e}")
            self.is_connected = False
    
    def set_mode(self, mode_name):
        """Установка режима полета"""
        try:
            # Получаем ID режима
            mode_id = self.connection.mode_mapping().get(mode_name.upper())
            if mode_id is None:
                logger.error(f"❌ Неизвестный режим: {mode_name}")
                return False
            
            logger.info(f"🔧 Установка режима: {mode_name} (ID: {mode_id})")
            
            # Отправляем команду смены режима
            self.connection.mav.set_mode_send(
                self.connection.target_system,
                mavutil.mavlink.MAV_MODE_FLAG_CUSTOM_MODE_ENABLED,
                mode_id
            )
            
            return True
            
        except Exception as e:
            logger.error(f"💥 Ошибка установки режима: {e}")
            return False
    
    def arm(self):
        """Включение моторов (ARM)"""
        try:
            logger.info("🔓 Включение моторов (ARM)")
            
            self.connection.mav.command_long_send(
                self.connection.target_system,
                self.connection.target_component,
                mavutil.mavlink.MAV_CMD_COMPONENT_ARM_DISARM,
                0,  # confirmation
                1,  # arm (1 = arm, 0 = disarm)
                0, 0, 0, 0, 0, 0  # unused parameters
            )
            
            self.armed = True
            logger.info("✅ Моторы включены")
            return True
            
        except Exception as e:
            logger.error(f"💥 Ошибка включения моторов: {e}")
            return False
    
    def disarm(self):
        """Выключение моторов (DISARM)"""
        try:
            logger.info("🔒 Выключение моторов (DISARM)")
            
            self.connection.mav.command_long_send(
                self.connection.target_system,
                self.connection.target_component,
                mavutil.mavlink.MAV_CMD_COMPONENT_ARM_DISARM,
                0,  # confirmation
                0,  # disarm (1 = arm, 0 = disarm)
                0, 0, 0, 0, 0, 0  # unused parameters
            )
            
            self.armed = False
            logger.info("✅ Моторы выключены")
            return True
            
        except Exception as e:
            logger.error(f"💥 Ошибка выключения моторов: {e}")
            return False
    
    def speed_to_pwm(self, speed):
        """
        Преобразование скорости (-1.0 до 1.0) в PWM значение
        
        :param speed: Скорость от -1.0 (полный назад) до 1.0 (полный вперед)
        :return: PWM значение в микросекундах
        """
        # Ограничиваем скорость
        speed = max(-1.0, min(1.0, speed))
        
        # Преобразуем в PWM
        if speed >= 0:
            # Вперед: от neutral до max
            pwm = self.pwm_neutral + (speed * (self.pwm_max - self.pwm_neutral))
        else:
            # Назад: от neutral до min
            pwm = self.pwm_neutral + (speed * (self.pwm_neutral - self.pwm_min))
        
        return int(pwm)
    
    def set_motors(self, left_speed, right_speed):
        """
        Установка скоростей моторов
        
        :param left_speed: Скорость левого мотора (-1.0 до 1.0)
        :param right_speed: Скорость правого мотора (-1.0 до 1.0)
        """
        self.current_left_speed = left_speed
        self.current_right_speed = right_speed
        
        logger.debug(f"🎮 Установка моторов: L={left_speed:.2f}, R={right_speed:.2f}")
    
    def set_motor_speed(self, motor_side, speed):
        """
        Установка скорости одного мотора (совместимость со старым API)
        
        :param motor_side: "left" или "right"
        :param speed: Скорость от -1.0 до 1.0
        """
        if motor_side == "left":
            self.set_motors(speed, self.current_right_speed)
        elif motor_side == "right":
            self.set_motors(self.current_left_speed, speed)
    
    def send_pwm_commands(self):
        """Отправка PWM команд в Pixhawk"""
        if not self.is_connected:
            return
        
        try:
            # Преобразуем скорости в PWM
            left_pwm = self.speed_to_pwm(self.current_left_speed)
            right_pwm = self.speed_to_pwm(self.current_right_speed)
            
            # Создаем массив PWM значений для всех каналов (1-8)
            pwm_values = [65535] * 8  # 65535 = игнорировать канал
            
            # Устанавливаем PWM для левых колес (каналы 1,2)
            for channel in self.left_motor_channels:
                pwm_values[channel - 1] = left_pwm
            
            # Устанавливаем PWM для правых колес (каналы 3,4)
            for channel in self.right_motor_channels:
                pwm_values[channel - 1] = right_pwm
            
            # Отправляем команду RC_CHANNELS_OVERRIDE
            self.connection.mav.rc_channels_override_send(
                self.connection.target_system,
                self.connection.target_component,
                *pwm_values  # Распаковываем массив в 8 параметров
            )
            
            logger.debug(f"📡 PWM отправлен: L={left_pwm}μs (каналы {self.left_motor_channels}), R={right_pwm}μs (каналы {self.right_motor_channels})")
            
        except Exception as e:
            logger.error(f"💥 Ошибка отправки PWM: {e}")
    
    def start_command_thread(self):
        """Запуск потока для периодической отправки команд"""
        if self.command_thread and self.command_thread.is_alive():
            return
        
        self.running = True
        self.command_thread = threading.Thread(target=self._command_loop, daemon=True)
        self.command_thread.start()
        logger.info("🔄 Поток команд запущен")
    
    def _command_loop(self):
        """Основной цикл отправки команд"""
        while self.running and self.is_connected:
            try:
                # Отправляем PWM команды с частотой 20 Гц
                self.send_pwm_commands()
                time.sleep(0.05)  # 50ms = 20 Гц
                
            except Exception as e:
                logger.error(f"💥 Ошибка в цикле команд: {e}")
                time.sleep(0.1)
    
    def stop(self):
        """Остановка всех моторов"""
        logger.info("🛑 Остановка моторов")
        self.set_motors(0.0, 0.0)
        time.sleep(0.1)  # Даем время отправить команду остановки
    
    def cleanup(self):
        """Очистка ресурсов"""
        logger.info("🧹 Очистка ресурсов Pixhawk контроллера")
        
        # Останавливаем поток команд
        self.running = False
        if self.command_thread and self.command_thread.is_alive():
            self.command_thread.join(timeout=1.0)
        
        # Останавливаем моторы
        self.stop()
        
        # Выключаем моторы
        if self.armed:
            self.disarm()
        
        # Закрываем соединение
        if self.connection:
            self.connection.close()
        
        logger.info("✅ Очистка завершена")

# Тестирование контроллера
if __name__ == "__main__":
    try:
        # Создаем контроллер (измените порт при необходимости)
        controller = PixhawkMotorController("/dev/ttyUSB0", 57600)
        
        if not controller.is_connected:
            print("❌ Не удалось подключиться к Pixhawk")
            exit(1)
        
        print("✅ Pixhawk подключен!")
        print("🔧 Тестирование моторов...")
        
        # Включаем моторы
        controller.arm()
        time.sleep(2)
        
        # Тест движения вперед
        print("➡️ Движение вперед")
        controller.set_motors(0.3, 0.3)
        time.sleep(3)
        
        # Остановка
        print("⏹️ Остановка")
        controller.stop()
        time.sleep(2)
        
        # Поворот влево
        print("↪️ Поворот влево")
        controller.set_motors(-0.2, 0.2)
        time.sleep(2)
        
        # Поворот вправо
        print("↩️ Поворот вправо")
        controller.set_motors(0.2, -0.2)
        time.sleep(2)
        
        # Финальная остановка
        print("🛑 Финальная остановка")
        controller.stop()
        time.sleep(1)
        
        print("✅ Тест завершен!")
        
    except KeyboardInterrupt:
        print("\n⏹️ Тест прерван пользователем")
    except Exception as e:
        print(f"💥 Ошибка: {e}")
    finally:
        if 'controller' in locals():
            controller.cleanup() 