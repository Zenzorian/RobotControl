#!/usr/bin/env python3
"""
Простой тест камеры для проверки работы веб-камеры на роботе
"""

import cv2
import time
import sys

def test_camera(camera_index=0):
    """Тестирование камеры"""
    print(f"🎥 Тестирование камеры {camera_index}...")
    
    # Попытка подключения к камере
    camera = cv2.VideoCapture(camera_index)
    
    if not camera.isOpened():
        print(f"❌ Камера {camera_index} не найдена!")
        return False
    
    # Получение информации о камере
    width = camera.get(cv2.CAP_PROP_FRAME_WIDTH)
    height = camera.get(cv2.CAP_PROP_FRAME_HEIGHT)
    fps = camera.get(cv2.CAP_PROP_FPS)
    
    print(f"✅ Камера найдена!")
    print(f"📐 Разрешение: {int(width)}x{int(height)}")
    print(f"🎬 FPS: {fps}")
    
    # Тест захвата кадров
    print("\n🔄 Тестирование захвата кадров...")
    
    successful_frames = 0
    total_attempts = 10
    
    for i in range(total_attempts):
        ret, frame = camera.read()
        if ret:
            successful_frames += 1
            print(f"✅ Кадр {i+1}: OK ({frame.shape[1]}x{frame.shape[0]})")
        else:
            print(f"❌ Кадр {i+1}: ОШИБКА")
        
        time.sleep(0.1)
    
    camera.release()
    
    success_rate = (successful_frames / total_attempts) * 100
    print(f"\n📊 Результат: {successful_frames}/{total_attempts} кадров ({success_rate:.1f}%)")
    
    if success_rate >= 80:
        print("✅ Камера работает хорошо!")
        return True
    else:
        print("⚠️ Проблемы с камерой!")
        return False

def find_all_cameras():
    """Поиск всех доступных камер"""
    print("🔍 Поиск всех доступных камер...")
    
    found_cameras = []
    
    for i in range(10):  # Проверяем индексы 0-9
        camera = cv2.VideoCapture(i)
        if camera.isOpened():
            ret, frame = camera.read()
            if ret:
                found_cameras.append(i)
                print(f"✅ Камера {i}: Найдена и работает")
            else:
                print(f"⚠️ Камера {i}: Найдена, но не захватывает кадры")
            camera.release()
        else:
            # Не выводим сообщение для несуществующих камер
            pass
    
    if not found_cameras:
        print("❌ Камеры не найдены!")
    else:
        print(f"\n📋 Найдено камер: {len(found_cameras)}")
        print(f"📍 Индексы: {found_cameras}")
    
    return found_cameras

def main():
    print("🤖 Тест камеры робота")
    print("=" * 40)
    
    # Поиск всех камер
    cameras = find_all_cameras()
    
    if not cameras:
        print("\n❌ Камеры не найдены. Проверьте подключение!")
        sys.exit(1)
    
    print("\n" + "=" * 40)
    
    # Тестирование первой найденной камеры
    camera_index = cameras[0]
    success = test_camera(camera_index)
    
    if success:
        print(f"\n✅ Камера {camera_index} готова к использованию!")
        print(f"💡 Запустите optimized_video_client.py с параметром --camera {camera_index}")
    else:
        print(f"\n❌ Проблемы с камерой {camera_index}")
        
        # Попробуем другие камеры
        if len(cameras) > 1:
            print("🔄 Пробуем другие камеры...")
            for cam_idx in cameras[1:]:
                if test_camera(cam_idx):
                    print(f"\n✅ Камера {cam_idx} работает!")
                    print(f"💡 Используйте параметр --camera {cam_idx}")
                    break

if __name__ == "__main__":
    main() 