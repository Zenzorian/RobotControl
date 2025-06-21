#!/usr/bin/env python3
"""
Автоматическое определение порта Pixhawk на Raspberry Pi
"""

import os
import glob
import serial
import time
from pymavlink import mavutil

def find_serial_ports():
    """Найти все доступные последовательные порты"""
    ports = []
    
    # Проверяем стандартные порты для Raspberry Pi
    possible_ports = [
        '/dev/ttyACM*',    # USB CDC устройства (обычно Pixhawk)
        '/dev/ttyUSB*',    # USB-Serial адаптеры
        '/dev/ttyAMA*',    # UART порты Raspberry Pi
        '/dev/serial/by-id/*'  # Порты по ID устройства
    ]
    
    for pattern in possible_ports:
        ports.extend(glob.glob(pattern))
    
    return sorted(ports)

def test_port_permissions(port):
    """Проверить права доступа к порту"""
    try:
        # Проверяем, можем ли мы открыть порт
        with serial.Serial(port, 9600, timeout=0.1):
            pass
        return True
    except serial.SerialException as e:
        if "Permission denied" in str(e):
            return False
        return True  # Другие ошибки могут быть нормальными
    except Exception:
        return True

def test_mavlink_connection(port, baudrate=57600, timeout=5):
    """Тестировать MAVLink соединение на порту"""
    try:
        print(f"  🔍 Тестирование MAVLink на {port}:{baudrate}...")
        
        # Создаем соединение с коротким таймаутом
        connection = mavutil.mavlink_connection(port, baud=baudrate)
        
        # Ждем heartbeat с таймаутом
        start_time = time.time()
        while time.time() - start_time < timeout:
            msg = connection.recv_match(type='HEARTBEAT', blocking=False)
            if msg:
                print(f"  ✅ MAVLink heartbeat получен!")
                print(f"     System ID: {msg.get_srcSystem()}")
                print(f"     Component ID: {msg.get_srcComponent()}")
                print(f"     Type: {msg.type}")
                print(f"     Autopilot: {msg.autopilot}")
                connection.close()
                return True
            time.sleep(0.1)
        
        connection.close()
        print(f"  ❌ Нет MAVLink heartbeat за {timeout}с")
        return False
        
    except Exception as e:
        print(f"  ❌ Ошибка MAVLink: {e}")
        return False

def get_port_info(port):
    """Получить информацию о порту"""
    info = {"port": port, "exists": False, "readable": False, "device_info": ""}
    
    # Проверяем существование
    if os.path.exists(port):
        info["exists"] = True
        
        # Проверяем права доступа
        info["readable"] = test_port_permissions(port)
        
        # Пытаемся получить информацию об устройстве
        try:
            if '/dev/serial/by-id/' in port:
                # Для портов by-id имя уже содержит информацию
                info["device_info"] = os.path.basename(port)
            else:
                # Пытаемся получить информацию через udev
                import subprocess
                result = subprocess.run(['udevadm', 'info', '--name=' + port], 
                                      capture_output=True, text=True)
                if result.returncode == 0:
                    for line in result.stdout.split('\n'):
                        if 'ID_VENDOR' in line or 'ID_MODEL' in line:
                            info["device_info"] += line.split('=')[1] + " "
        except:
            pass
    
    return info

def main():
    print("🔍 Поиск Pixhawk на Raspberry Pi")
    print("=" * 50)
    
    # Находим все порты
    ports = find_serial_ports()
    
    if not ports:
        print("❌ Последовательные порты не найдены!")
        print("\n💡 Проверьте:")
        print("   - Подключен ли Pixhawk к USB")
        print("   - Включен ли Pixhawk")
        return
    
    print(f"📋 Найдено портов: {len(ports)}")
    print("-" * 50)
    
    pixhawk_candidates = []
    
    # Анализируем каждый порт
    for port in ports:
        print(f"\n🔌 Порт: {port}")
        info = get_port_info(port)
        
        if not info["exists"]:
            print("  ❌ Порт не существует")
            continue
        
        if not info["readable"]:
            print("  ⚠️  Нет прав доступа")
            print("     Выполните: sudo chmod 666 " + port)
            print("     Или добавьте пользователя в группу dialout:")
            print("     sudo usermod -a -G dialout $USER")
            continue
        
        print("  ✅ Порт доступен")
        if info["device_info"]:
            print(f"  📝 Устройство: {info['device_info'].strip()}")
        
        # Тестируем разные скорости
        baudrates = [57600, 115200, 9600]
        for baud in baudrates:
            if test_mavlink_connection(port, baud, timeout=3):
                pixhawk_candidates.append((port, baud))
                print(f"  🎯 Pixhawk найден на {port}:{baud}")
                break
    
    # Результаты
    print("\n" + "=" * 50)
    print("📊 РЕЗУЛЬТАТЫ:")
    
    if not pixhawk_candidates:
        print("❌ Pixhawk не найден!")
        print("\n🔧 Возможные причины:")
        print("   1. Pixhawk не подключен или выключен")
        print("   2. Неправильный USB кабель (нужен data-кабель)")
        print("   3. Pixhawk не настроен для MAVLink")
        print("   4. Неправильная скорость соединения")
        print("\n💡 Попробуйте:")
        print("   - Переподключить USB кабель")
        print("   - Проверить питание Pixhawk")
        print("   - Использовать QGroundControl для проверки")
    else:
        print("✅ Найдены Pixhawk устройства:")
        for i, (port, baud) in enumerate(pixhawk_candidates, 1):
            print(f"   {i}. {port} на {baud} baud")
        
        # Рекомендуемые команды запуска
        best_port, best_baud = pixhawk_candidates[0]
        print(f"\n🚀 Рекомендуемая команда запуска:")
        print(f"python3 robot_client.py --use-motors --pixhawk --pixhawk-port {best_port} --pixhawk-baud {best_baud}")
        
        print(f"\n🧪 Команда для тестирования:")
        print(f"python3 test_4wheel_pixhawk.py")
        print(f"   (используйте порт: {best_port}, скорость: {best_baud})")

if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n⏹️ Поиск прерван")
    except Exception as e:
        print(f"\n💥 Ошибка: {e}")
        import traceback
        traceback.print_exc() 