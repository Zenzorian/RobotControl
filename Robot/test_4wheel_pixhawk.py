#!/usr/bin/env python3
"""
Тест 4-колесной конфигурации Pixhawk 6C
Каналы 1,2 - левые колеса
Каналы 3,4 - правые колеса
"""

import time
import sys
from pixhawk_motor_controller import PixhawkMotorController

def test_individual_wheels(controller):
    """Тест каждого колеса по отдельности"""
    print("\n🔧 Тест отдельных колес:")
    
    # Включаем моторы
    if not controller.armed:
        controller.arm()
        time.sleep(2)
    
    test_speed = 0.3
    test_duration = 2
    
    # Тест левого переднего (канал 1)
    print("🔄 Тест левого переднего колеса (канал 1)")
    controller.connection.mav.rc_channels_override_send(
        controller.connection.target_system,
        controller.connection.target_component,
        controller.speed_to_pwm(test_speed),  # Канал 1
        65535, 65535, 65535, 65535, 65535, 65535, 65535
    )
    time.sleep(test_duration)
    
    # Остановка
    controller.stop()
    time.sleep(1)
    
    # Тест левого заднего (канал 2)
    print("🔄 Тест левого заднего колеса (канал 2)")
    controller.connection.mav.rc_channels_override_send(
        controller.connection.target_system,
        controller.connection.target_component,
        65535,  # Канал 1
        controller.speed_to_pwm(test_speed),  # Канал 2
        65535, 65535, 65535, 65535, 65535, 65535
    )
    time.sleep(test_duration)
    
    # Остановка
    controller.stop()
    time.sleep(1)
    
    # Тест правого переднего (канал 3)
    print("🔄 Тест правого переднего колеса (канал 3)")
    controller.connection.mav.rc_channels_override_send(
        controller.connection.target_system,
        controller.connection.target_component,
        65535, 65535,  # Каналы 1,2
        controller.speed_to_pwm(test_speed),  # Канал 3
        65535, 65535, 65535, 65535, 65535
    )
    time.sleep(test_duration)
    
    # Остановка
    controller.stop()
    time.sleep(1)
    
    # Тест правого заднего (канал 4)
    print("🔄 Тест правого заднего колеса (канал 4)")
    controller.connection.mav.rc_channels_override_send(
        controller.connection.target_system,
        controller.connection.target_component,
        65535, 65535, 65535,  # Каналы 1,2,3
        controller.speed_to_pwm(test_speed),  # Канал 4
        65535, 65535, 65535, 65535
    )
    time.sleep(test_duration)
    
    # Финальная остановка
    controller.stop()
    time.sleep(1)

def test_differential_drive(controller):
    """Тест дифференциального управления"""
    print("\n🎮 Тест дифференциального управления:")
    
    # Включаем моторы
    if not controller.armed:
        controller.arm()
        time.sleep(2)
    
    # Движение вперед
    print("➡️ Движение вперед (все колеса)")
    controller.set_motors(0.4, 0.4)
    time.sleep(3)
    
    # Остановка
    print("⏹️ Остановка")
    controller.stop()
    time.sleep(2)
    
    # Поворот влево (левые колеса назад, правые вперед)
    print("↪️ Поворот влево")
    controller.set_motors(-0.3, 0.3)
    time.sleep(2)
    
    # Поворот вправо (левые колеса вперед, правые назад)
    print("↩️ Поворот вправо")
    controller.set_motors(0.3, -0.3)
    time.sleep(2)
    
    # Движение назад
    print("⬅️ Движение назад")
    controller.set_motors(-0.4, -0.4)
    time.sleep(2)
    
    # Финальная остановка
    print("🛑 Финальная остановка")
    controller.stop()
    time.sleep(1)

def main():
    print("🚁 Тест 4-колесной конфигурации Pixhawk 6C")
    print("=" * 50)
    print("Конфигурация:")
    print("  PWM 1,2 → Левые колеса")
    print("  PWM 3,4 → Правые колеса")
    print("=" * 50)
    
    # Запрашиваем порт
    port = input("Введите порт Pixhawk (по умолчанию /dev/ttyUSB0): ").strip()
    if not port:
        port = "/dev/ttyUSB0"
    
    # Запрашиваем скорость
    baud_input = input("Введите скорость (по умолчанию 57600): ").strip()
    try:
        baudrate = int(baud_input) if baud_input else 57600
    except ValueError:
        baudrate = 57600
    
    try:
        # Создаем контроллер
        print(f"\n🔗 Подключение к Pixhawk: {port}:{baudrate}")
        controller = PixhawkMotorController(port, baudrate)
        
        if not controller.is_connected:
            print("❌ Не удалось подключиться к Pixhawk")
            return 1
        
        print("✅ Pixhawk подключен!")
        
        # Выбор типа теста
        print("\nВыберите тип теста:")
        print("1. Тест отдельных колес")
        print("2. Тест дифференциального управления")
        print("3. Оба теста")
        
        choice = input("Ваш выбор (1-3): ").strip()
        
        if choice in ['1', '3']:
            test_individual_wheels(controller)
        
        if choice in ['2', '3']:
            test_differential_drive(controller)
        
        print("\n✅ Тестирование завершено!")
        
    except KeyboardInterrupt:
        print("\n⏹️ Тест прерван пользователем")
    except Exception as e:
        print(f"💥 Ошибка: {e}")
        return 1
    finally:
        if 'controller' in locals():
            controller.cleanup()
    
    return 0

if __name__ == "__main__":
    sys.exit(main()) 