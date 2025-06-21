#!/usr/bin/env python3
"""
Тест интерпретации команд управления роботом
Проверяет правильность преобразования джойстика в команды моторов
"""

import json
import time
import sys
from pixhawk_motor_controller import PixhawkMotorController

class CommandTester:
    def __init__(self, motor_controller=None):
        self.motor_controller = motor_controller
        
    def _calculate_motor_speeds(self, x, y):
        """Копия функции из robot_client.py для тестирования"""
        # Припускаємо, що x та y мають значення від -1 до 1
        # Проста диференціальна схема керування для руху
        left_speed = y + x
        right_speed = y - x
        
        # Обмежуємо швидкості в діапазоні від -1 до 1
        left_speed = max(-1, min(1, left_speed))
        right_speed = max(-1, min(1, right_speed))
        
        return left_speed, right_speed
    
    def test_command_interpretation(self):
        """Тест различных команд джойстика"""
        print("🎮 Тест интерпретации команд управления")
        print("=" * 60)
        
        # Тестовые команды (x, y, описание)
        test_commands = [
            (0.0, 0.0, "Остановка (центр джойстика)"),
            (0.0, 1.0, "Движение вперед"),
            (0.0, -1.0, "Движение назад"),
            (1.0, 0.0, "Поворот вправо на месте"),
            (-1.0, 0.0, "Поворот влево на месте"),
            (0.5, 0.5, "Движение вперед с поворотом вправо"),
            (-0.5, 0.5, "Движение вперед с поворотом влево"),
            (0.5, -0.5, "Движение назад с поворотом вправо"),
            (-0.5, -0.5, "Движение назад с поворотом влево"),
            (1.0, 1.0, "Максимальный поворот вправо вперед"),
            (-1.0, 1.0, "Максимальный поворот влево вперед"),
        ]
        
        print(f"{'Команда':<40} {'Левый мотор':<12} {'Правый мотор':<12} {'PWM Левый':<12} {'PWM Правый':<12}")
        print("-" * 88)
        
        for x, y, description in test_commands:
            left_speed, right_speed = self._calculate_motor_speeds(x, y)
            
            # Если есть контроллер, показываем PWM значения
            if self.motor_controller:
                left_pwm = self.motor_controller.speed_to_pwm(left_speed)
                right_pwm = self.motor_controller.speed_to_pwm(right_speed)
                pwm_left_str = f"{left_pwm}μs"
                pwm_right_str = f"{right_pwm}μs"
            else:
                pwm_left_str = "N/A"
                pwm_right_str = "N/A"
            
            print(f"{description:<40} {left_speed:>8.2f}    {right_speed:>8.2f}    {pwm_left_str:>10} {pwm_right_str:>10}")
    
    def test_unity_command_format(self):
        """Тест формата команд от Unity"""
        print("\n🎯 Тест формата команд Unity")
        print("=" * 60)
        
        # Примеры команд от Unity
        unity_commands = [
            {
                "leftStickValue": {"x": 0.0, "y": 0.0},
                "rightStickValue": {"x": 0.0, "y": 0.0},
                "cameraAngle": 90.0
            },
            {
                "leftStickValue": {"x": 0.0, "y": 0.8},
                "rightStickValue": {"x": 0.0, "y": 0.0},
                "cameraAngle": 90.0
            },
            {
                "leftStickValue": {"x": 0.6, "y": 0.4},
                "rightStickValue": {"x": 0.0, "y": 0.0},
                "cameraAngle": 45.0
            },
            {
                "leftStickValue": {"x": -0.8, "y": 0.0},
                "rightStickValue": {"x": 0.0, "y": 0.0},
                "cameraAngle": 135.0
            }
        ]
        
        print("Unity команда → Интерпретация")
        print("-" * 60)
        
        for i, cmd in enumerate(unity_commands, 1):
            left_stick = cmd["leftStickValue"]
            x, y = left_stick["x"], left_stick["y"]
            left_speed, right_speed = self._calculate_motor_speeds(x, y)
            
            print(f"\nКоманда {i}:")
            print(f"  Левый стик: x={x:>6.2f}, y={y:>6.2f}")
            print(f"  Угол камеры: {cmd['cameraAngle']:>6.1f}°")
            print(f"  → Левый мотор: {left_speed:>6.2f}")
            print(f"  → Правый мотор: {right_speed:>6.2f}")
            
            if self.motor_controller:
                left_pwm = self.motor_controller.speed_to_pwm(left_speed)
                right_pwm = self.motor_controller.speed_to_pwm(right_speed)
                print(f"  → PWM: L={left_pwm}μs, R={right_pwm}μs")
    
    def test_live_commands(self):
        """Интерактивный тест команд"""
        print("\n🕹️ Интерактивный тест команд")
        print("=" * 60)
        print("Введите команды в формате: x y")
        print("Где x и y от -1.0 до 1.0")
        print("Примеры: '0 1' (вперед), '1 0' (поворот вправо), 'q' (выход)")
        print("-" * 60)
        
        while True:
            try:
                user_input = input("\nВведите команду (x y): ").strip()
                
                if user_input.lower() in ['q', 'quit', 'exit']:
                    break
                
                parts = user_input.split()
                if len(parts) != 2:
                    print("❌ Неверный формат. Используйте: x y")
                    continue
                
                x = float(parts[0])
                y = float(parts[1])
                
                # Ограничиваем значения
                x = max(-1.0, min(1.0, x))
                y = max(-1.0, min(1.0, y))
                
                left_speed, right_speed = self._calculate_motor_speeds(x, y)
                
                print(f"📊 Джойстик: x={x:>6.2f}, y={y:>6.2f}")
                print(f"🎮 Моторы: L={left_speed:>6.2f}, R={right_speed:>6.2f}")
                
                if self.motor_controller:
                    left_pwm = self.motor_controller.speed_to_pwm(left_speed)
                    right_pwm = self.motor_controller.speed_to_pwm(right_speed)
                    print(f"📡 PWM: L={left_pwm}μs, R={right_pwm}μs")
                    
                    # Отправляем команду на реальный контроллер (если подключен)
                    if self.motor_controller.is_connected:
                        print("🚀 Отправка команды на Pixhawk...")
                        self.motor_controller.set_motors(left_speed, right_speed)
                        time.sleep(0.1)  # Небольшая задержка
                
            except ValueError:
                print("❌ Неверные числа. Используйте числа от -1.0 до 1.0")
            except KeyboardInterrupt:
                break
            except Exception as e:
                print(f"❌ Ошибка: {e}")
    
    def test_movement_patterns(self):
        """Тест паттернов движения"""
        print("\n🔄 Тест паттернов движения")
        print("=" * 60)
        
        if not self.motor_controller or not self.motor_controller.is_connected:
            print("⚠️ Pixhawk не подключен, показываем только расчеты")
            return
        
        patterns = [
            ("Квадрат", [
                (0, 0.5, 2),    # Вперед
                (0.5, 0, 1),    # Поворот вправо
                (0, 0.5, 2),    # Вперед
                (0.5, 0, 1),    # Поворот вправо
                (0, 0.5, 2),    # Вперед
                (0.5, 0, 1),    # Поворот вправо
                (0, 0.5, 2),    # Вперед
                (0.5, 0, 1),    # Поворот вправо
            ]),
            ("Восьмерка", [
                (0.3, 0.3, 2),   # Вперед-вправо
                (-0.3, 0.3, 2),  # Вперед-влево
                (0.3, 0.3, 2),   # Вперед-вправо
                (-0.3, 0.3, 2),  # Вперед-влево
            ])
        ]
        
        for pattern_name, moves in patterns:
            print(f"\n🎯 Паттерн: {pattern_name}")
            response = input("Выполнить? (y/n): ").strip().lower()
            
            if response == 'y':
                # Включаем моторы
                if not self.motor_controller.armed:
                    self.motor_controller.arm()
                    time.sleep(2)
                
                for i, (x, y, duration) in enumerate(moves, 1):
                    left_speed, right_speed = self._calculate_motor_speeds(x, y)
                    print(f"  Шаг {i}: x={x:>5.2f}, y={y:>5.2f} → L={left_speed:>5.2f}, R={right_speed:>5.2f} ({duration}с)")
                    
                    self.motor_controller.set_motors(left_speed, right_speed)
                    time.sleep(duration)
                
                # Остановка
                print("  🛑 Остановка")
                self.motor_controller.stop()
                time.sleep(1)

def main():
    print("🤖 Тестер интерпретации команд робота")
    print("=" * 60)
    
    # Спрашиваем, нужно ли подключаться к Pixhawk
    use_pixhawk = input("Подключиться к Pixhawk для реального тестирования? (y/n): ").strip().lower() == 'y'
    
    motor_controller = None
    if use_pixhawk:
        port = input("Порт Pixhawk (по умолчанию /dev/ttyUSB0): ").strip() or "/dev/ttyUSB0"
        baud_input = input("Скорость (по умолчанию 57600): ").strip()
        baudrate = int(baud_input) if baud_input else 57600
        
        try:
            print(f"🔗 Подключение к Pixhawk: {port}:{baudrate}")
            motor_controller = PixhawkMotorController(port, baudrate)
            
            if motor_controller.is_connected:
                print("✅ Pixhawk подключен!")
            else:
                print("❌ Не удалось подключиться к Pixhawk")
                motor_controller = None
        except Exception as e:
            print(f"❌ Ошибка подключения: {e}")
            motor_controller = None
    
    # Создаем тестер
    tester = CommandTester(motor_controller)
    
    while True:
        print("\n" + "=" * 60)
        print("Выберите тест:")
        print("1. Тест интерпретации команд")
        print("2. Тест формата Unity команд")
        print("3. Интерактивный тест")
        print("4. Тест паттернов движения")
        print("5. Выход")
        
        choice = input("\nВаш выбор (1-5): ").strip()
        
        if choice == '1':
            tester.test_command_interpretation()
        elif choice == '2':
            tester.test_unity_command_format()
        elif choice == '3':
            tester.test_live_commands()
        elif choice == '4':
            tester.test_movement_patterns()
        elif choice == '5':
            break
        else:
            print("❌ Неверный выбор")
    
    # Очистка
    if motor_controller:
        motor_controller.cleanup()
    
    print("\n✅ Тестирование завершено!")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n⏹️ Тест прерван пользователем")
    except Exception as e:
        print(f"\n💥 Ошибка: {e}")
    finally:
        print("🧹 Очистка ресурсов...") 