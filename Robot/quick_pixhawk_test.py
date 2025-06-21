#!/usr/bin/env python3
"""
Быстрый тест подключения к Pixhawk
"""

import sys
import time
from pixhawk_motor_controller import PixhawkMotorController

def quick_test():
    print("🚁 Быстрый тест Pixhawk")
    print("=" * 40)
    
    # Пробуем стандартные порты для Raspberry Pi
    test_ports = [
        ("/dev/ttyACM0", 57600),
        ("/dev/ttyACM1", 57600),
        ("/dev/ttyUSB0", 57600),
        ("/dev/ttyACM0", 115200),
    ]
    
    for port, baud in test_ports:
        print(f"\n🔍 Тестирование {port}:{baud}")
        
        try:
            controller = PixhawkMotorController(port, baud)
            
            if controller.is_connected:
                print(f"✅ Pixhawk найден на {port}:{baud}")
                
                # Быстрый тест команд
                print("🧪 Тест команд...")
                controller.set_motors(0.0, 0.0)  # Остановка
                time.sleep(0.5)
                
                print("✅ Команды работают!")
                print(f"\n🚀 Используйте для запуска:")
                print(f"python3 robot_client.py --use-motors --pixhawk --pixhawk-port {port} --pixhawk-baud {baud}")
                
                controller.cleanup()
                return True
            else:
                print(f"❌ Нет соединения")
                
        except Exception as e:
            print(f"❌ Ошибка: {e}")
    
    print(f"\n❌ Pixhawk не найден на стандартных портах")
    print(f"💡 Попробуйте: python3 detect_pixhawk_port.py")
    return False

if __name__ == "__main__":
    try:
        quick_test()
    except KeyboardInterrupt:
        print("\n⏹️ Тест прерван")
    except Exception as e:
        print(f"\n💥 Ошибка: {e}") 