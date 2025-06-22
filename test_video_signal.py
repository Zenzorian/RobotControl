#!/usr/bin/env python3
"""
Тест отправки видео сигнала через WebSocket
Проверяет, отправляется ли видео с робота на сервер
"""

import asyncio
import websockets
import json
import cv2
import base64
import time
import argparse
from datetime import datetime

class VideoSignalTester:
    def __init__(self, server_url, camera_index=0, test_mode=False):
        self.server_url = server_url
        self.camera_index = camera_index
        self.test_mode = test_mode
        self.camera = None
        self.frame_count = 0
        self.bytes_sent = 0
        self.start_time = time.time()
        
    def initialize_camera(self):
        """Инициализация камеры"""
        if self.test_mode:
            print("🧪 Тестовый режим - будут отправляться синтетические кадры")
            return True
            
        print(f"🎥 Подключение к камере {self.camera_index}...")
        self.camera = cv2.VideoCapture(self.camera_index)
        
        if not self.camera.isOpened():
            print(f"❌ Не удалось подключиться к камере {self.camera_index}")
            return False
            
        # Настройка камеры
        self.camera.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        self.camera.set(cv2.CAP_PROP_FPS, 15)
        self.camera.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        
        print("✅ Камера подключена успешно")
        return True
    
    def create_test_frame(self):
        """Создание тестового кадра"""
        import numpy as np
        
        frame = np.zeros((480, 640, 3), np.uint8)
        
        # Создаем градиент
        for y in range(480):
            for x in range(640):
                frame[y, x] = [
                    int(255 * x / 640),
                    int(255 * y / 480),
                    int(255 * ((x + y + self.frame_count) % 256) / 256)
                ]
        
        # Добавляем текст
        cv2.putText(frame, f"TEST FRAME", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
        cv2.putText(frame, f"Frame: {self.frame_count}", (50, 100), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
        cv2.putText(frame, datetime.now().strftime("%H:%M:%S"), (50, 130), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
        
        return frame
    
    def capture_frame(self):
        """Захват кадра"""
        if self.test_mode:
            return self.create_test_frame()
            
        if not self.camera or not self.camera.isOpened():
            return None
            
        ret, frame = self.camera.read()
        return frame if ret else None
    
    def encode_frame(self, frame):
        """Кодирование кадра в JPEG + Base64"""
        if frame is None:
            return None
            
        # Кодирование в JPEG
        encode_params = [cv2.IMWRITE_JPEG_QUALITY, 75]
        success, buffer = cv2.imencode('.jpg', frame, encode_params)
        
        if not success:
            return None
            
        # Кодирование в Base64
        encoded = base64.b64encode(buffer).decode('utf-8')
        return encoded
    
    async def test_video_transmission(self, duration=30):
        """Тест передачи видео"""
        print(f"🚀 Начинаем тест передачи видео на {duration} секунд...")
        print(f"🌐 Сервер: {self.server_url}")
        
        try:
            async with websockets.connect(self.server_url) as websocket:
                print("✅ Подключение к серверу установлено")
                
                # Регистрация как робот
                await websocket.send("REGISTER!ROBOT")
                print("📝 Зарегистрирован как ROBOT")
                
                start_time = time.time()
                last_stats_time = start_time
                
                while time.time() - start_time < duration:
                    # Захват и кодирование кадра
                    frame = self.capture_frame()
                    if frame is None:
                        print("❌ Ошибка захвата кадра")
                        await asyncio.sleep(0.1)
                        continue
                    
                    encoded_frame = self.encode_frame(frame)
                    if encoded_frame is None:
                        print("❌ Ошибка кодирования кадра")
                        await asyncio.sleep(0.1)
                        continue
                    
                    # Создание сообщения
                    video_message = {
                        'type': 'video_frame',
                        'data': encoded_frame,
                        'timestamp': time.time(),
                        'frame_number': self.frame_count
                    }
                    
                    # Отправка
                    try:
                        await websocket.send(json.dumps(video_message))
                        self.frame_count += 1
                        self.bytes_sent += len(encoded_frame)
                        
                        # Статистика каждые 5 секунд
                        current_time = time.time()
                        if current_time - last_stats_time >= 5.0:
                            elapsed = current_time - start_time
                            fps = self.frame_count / elapsed
                            mbps = (self.bytes_sent * 8) / (elapsed * 1024 * 1024)
                            
                            print(f"📊 Статистика: {self.frame_count} кадров, {fps:.1f} FPS, {mbps:.2f} Mbps")
                            last_stats_time = current_time
                        
                    except Exception as e:
                        print(f"❌ Ошибка отправки: {e}")
                    
                    # Задержка для контроля FPS
                    await asyncio.sleep(1/15)  # ~15 FPS
                
                # Финальная статистика
                total_time = time.time() - start_time
                avg_fps = self.frame_count / total_time
                avg_mbps = (self.bytes_sent * 8) / (total_time * 1024 * 1024)
                
                print("\n" + "="*50)
                print("📈 ИТОГОВАЯ СТАТИСТИКА:")
                print(f"⏱️  Время работы: {total_time:.1f} сек")
                print(f"🎬 Отправлено кадров: {self.frame_count}")
                print(f"📊 Средний FPS: {avg_fps:.1f}")
                print(f"💾 Передано данных: {self.bytes_sent / 1024 / 1024:.1f} МБ")
                print(f"🌐 Средняя скорость: {avg_mbps:.2f} Mbps")
                print("="*50)
                
        except Exception as e:
            print(f"❌ Ошибка подключения: {e}")
        finally:
            if self.camera:
                self.camera.release()

async def main():
    parser = argparse.ArgumentParser(description='Тест отправки видео сигнала')
    parser.add_argument('--server', default='ws://localhost:3000', 
                       help='URL WebSocket сервера')
    parser.add_argument('--camera', type=int, default=0, 
                       help='Индекс камеры')
    parser.add_argument('--test-mode', action='store_true', 
                       help='Тестовый режим без камеры')
    parser.add_argument('--duration', type=int, default=30, 
                       help='Длительность теста в секундах')
    
    args = parser.parse_args()
    
    print("🤖 ТЕСТ ОТПРАВКИ ВИДЕО СИГНАЛА")
    print("="*50)
    
    tester = VideoSignalTester(args.server, args.camera, args.test_mode)
    
    if not tester.initialize_camera():
        print("❌ Не удалось инициализировать камеру")
        print("💡 Попробуйте запустить с --test-mode для тестового режима")
        return
    
    await tester.test_video_transmission(args.duration)

if __name__ == "__main__":
    asyncio.run(main()) 