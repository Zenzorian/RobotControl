#!/usr/bin/env python3
"""
Скрипт для тестирования производительности оптимизированной системы передачи видео
"""

import asyncio
import time
import cv2
import numpy as np
import base64
import psutil
import json
from threading import Thread
import argparse

class PerformanceTest:
    def __init__(self):
        self.test_results = {
            'encoding_times': [],
            'frame_sizes': [],
            'cpu_usage': [],
            'memory_usage': [],
            'total_frames': 0,
            'start_time': None,
            'end_time': None
        }
    
    def test_jpeg_encoding(self, resolution=(640, 480), quality=75, num_frames=100):
        """Тест производительности JPEG кодирования"""
        print(f"Тестирование JPEG кодирования: {resolution}, качество {quality}")
        
        # Создаем тестовый кадр
        test_frame = self._create_test_frame(resolution)
        
        encode_params = [cv2.IMWRITE_JPEG_QUALITY, quality]
        
        self.test_results['start_time'] = time.time()
        
        for i in range(num_frames):
            start_time = time.time()
            
            # Кодируем в JPEG
            success, buffer = cv2.imencode('.jpg', test_frame, encode_params)
            
            if success:
                # Конвертируем в base64
                encoded_data = base64.b64encode(buffer).decode('utf-8')
                
                encoding_time = time.time() - start_time
                frame_size = len(encoded_data)
                
                self.test_results['encoding_times'].append(encoding_time)
                self.test_results['frame_sizes'].append(frame_size)
                self.test_results['total_frames'] += 1
                
                # Мониторинг системных ресурсов
                cpu_percent = psutil.cpu_percent()
                memory_percent = psutil.virtual_memory().percent
                
                self.test_results['cpu_usage'].append(cpu_percent)
                self.test_results['memory_usage'].append(memory_percent)
                
                if i % 10 == 0:
                    print(f"Кадр {i}: {encoding_time*1000:.1f}мс, {frame_size}B, CPU: {cpu_percent:.1f}%")
            else:
                print(f"Ошибка кодирования кадра {i}")
        
        self.test_results['end_time'] = time.time()
    
    def _create_test_frame(self, resolution):
        """Создание тестового кадра"""
        frame = np.zeros((resolution[1], resolution[0], 3), np.uint8)
        
        # Создаем детализированное изображение для реалистичного теста
        for y in range(resolution[1]):
            for x in range(resolution[0]):
                frame[y, x] = [
                    int(255 * np.sin(x * 0.01) * 0.5 + 127),
                    int(255 * np.cos(y * 0.01) * 0.5 + 127),
                    int(255 * np.sin((x + y) * 0.005) * 0.5 + 127)
                ]
        
        # Добавляем текст и линии для большей детализации
        cv2.putText(frame, "PERFORMANCE TEST", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
        cv2.line(frame, (0, 0), resolution, (255, 0, 0), 2)
        cv2.line(frame, (resolution[0], 0), (0, resolution[1]), (0, 255, 0), 2)
        
        return frame
    
    def test_realtime_encoding(self, resolution=(640, 480), quality=75, duration=30, target_fps=15):
        """Тест кодирования в реальном времени"""
        print(f"Тест реального времени: {duration}с @ {target_fps} FPS")
        
        frame_interval = 1.0 / target_fps
        test_frame = self._create_test_frame(resolution)
        encode_params = [cv2.IMWRITE_JPEG_QUALITY, quality]
        
        start_time = time.time()
        last_frame_time = start_time
        frame_count = 0
        dropped_frames = 0
        
        while time.time() - start_time < duration:
            current_time = time.time()
            
            if current_time - last_frame_time >= frame_interval:
                encode_start = time.time()
                
                # Кодируем кадр
                success, buffer = cv2.imencode('.jpg', test_frame, encode_params)
                
                if success:
                    encoded_data = base64.b64encode(buffer).decode('utf-8')
                    encoding_time = time.time() - encode_start
                    
                    if encoding_time > frame_interval:
                        dropped_frames += 1
                    
                    frame_count += 1
                    last_frame_time = current_time
                    
                    # Статистика каждые 5 секунд
                    if frame_count % (target_fps * 5) == 0:
                        elapsed = current_time - start_time
                        actual_fps = frame_count / elapsed
                        print(f"t={elapsed:.1f}с: {actual_fps:.1f} FPS, пропущено: {dropped_frames}")
                
            time.sleep(0.001)  # Небольшая пауза
        
        total_time = time.time() - start_time
        actual_fps = frame_count / total_time
        drop_rate = (dropped_frames / frame_count) * 100 if frame_count > 0 else 0
        
        print(f"Результат: {actual_fps:.1f} FPS, пропущено {drop_rate:.1f}%")
        
        return {
            'actual_fps': actual_fps,
            'target_fps': target_fps,
            'total_frames': frame_count,
            'dropped_frames': dropped_frames,
            'drop_rate': drop_rate,
            'duration': total_time
        }
    
    def test_different_qualities(self, resolution=(640, 480)):
        """Тест разных уровней качества"""
        qualities = [30, 50, 70, 85, 95]
        results = {}
        
        print("Тестирование разных уровней качества JPEG:")
        
        for quality in qualities:
            print(f"\nКачество {quality}:")
            
            # Сброс результатов
            self.test_results = {
                'encoding_times': [],
                'frame_sizes': [],
                'cpu_usage': [],
                'memory_usage': [],
                'total_frames': 0
            }
            
            # Тест кодирования
            self.test_jpeg_encoding(resolution, quality, 50)
            
            # Анализ результатов
            avg_time = np.mean(self.test_results['encoding_times']) * 1000
            avg_size = np.mean(self.test_results['frame_sizes'])
            max_fps = 1.0 / max(self.test_results['encoding_times'])
            
            results[quality] = {
                'avg_encoding_time_ms': avg_time,
                'avg_frame_size_bytes': avg_size,
                'max_fps': max_fps
            }
            
            print(f"  Среднее время кодирования: {avg_time:.1f}мс")
            print(f"  Средний размер кадра: {avg_size:.0f}B")
            print(f"  Максимальный FPS: {max_fps:.1f}")
        
        return results
    
    def test_different_resolutions(self, quality=75):
        """Тест разных разрешений"""
        resolutions = [
            (320, 240),   # QVGA
            (480, 360),   # 480p
            (640, 480),   # VGA
            (800, 600),   # SVGA
            (1024, 768)   # XGA
        ]
        
        results = {}
        
        print("Тестирование разных разрешений:")
        
        for resolution in resolutions:
            print(f"\nРазрешение {resolution[0]}x{resolution[1]}:")
            
            # Тест кодирования
            self.test_results = {
                'encoding_times': [],
                'frame_sizes': [],
                'cpu_usage': [],
                'memory_usage': [],
                'total_frames': 0
            }
            
            self.test_jpeg_encoding(resolution, quality, 30)
            
            # Анализ результатов
            avg_time = np.mean(self.test_results['encoding_times']) * 1000
            avg_size = np.mean(self.test_results['frame_sizes'])
            max_fps = 1.0 / max(self.test_results['encoding_times'])
            
            results[f"{resolution[0]}x{resolution[1]}"] = {
                'avg_encoding_time_ms': avg_time,
                'avg_frame_size_bytes': avg_size,
                'max_fps': max_fps
            }
            
            print(f"  Среднее время кодирования: {avg_time:.1f}мс")
            print(f"  Средний размер кадра: {avg_size:.0f}B")
            print(f"  Максимальный FPS: {max_fps:.1f}")
        
        return results
    
    def generate_report(self):
        """Генерация отчета о производительности"""
        if not self.test_results['encoding_times']:
            print("Нет данных для отчета")
            return
        
        # Статистика кодирования
        encoding_times = np.array(self.test_results['encoding_times']) * 1000  # в мс
        frame_sizes = np.array(self.test_results['frame_sizes'])
        
        total_duration = self.test_results['end_time'] - self.test_results['start_time']
        actual_fps = self.test_results['total_frames'] / total_duration
        
        report = {
            'summary': {
                'total_frames': self.test_results['total_frames'],
                'test_duration_sec': total_duration,
                'actual_fps': actual_fps
            },
            'encoding_performance': {
                'avg_time_ms': float(np.mean(encoding_times)),
                'min_time_ms': float(np.min(encoding_times)),
                'max_time_ms': float(np.max(encoding_times)),
                'std_time_ms': float(np.std(encoding_times))
            },
            'frame_sizes': {
                'avg_size_bytes': float(np.mean(frame_sizes)),
                'min_size_bytes': int(np.min(frame_sizes)),
                'max_size_bytes': int(np.max(frame_sizes)),
                'total_data_mb': float(np.sum(frame_sizes)) / (1024 * 1024)
            },
            'system_usage': {
                'avg_cpu_percent': float(np.mean(self.test_results['cpu_usage'])),
                'max_cpu_percent': float(np.max(self.test_results['cpu_usage'])),
                'avg_memory_percent': float(np.mean(self.test_results['memory_usage']))
            }
        }
        
        print("\n" + "="*50)
        print("ОТЧЕТ О ПРОИЗВОДИТЕЛЬНОСТИ")
        print("="*50)
        print(f"Общие данные:")
        print(f"  Кадров обработано: {report['summary']['total_frames']}")
        print(f"  Длительность теста: {report['summary']['test_duration_sec']:.1f}с")
        print(f"  Фактический FPS: {report['summary']['actual_fps']:.1f}")
        
        print(f"\nПроизводительность кодирования:")
        print(f"  Среднее время: {report['encoding_performance']['avg_time_ms']:.1f}мс")
        print(f"  Минимальное время: {report['encoding_performance']['min_time_ms']:.1f}мс")
        print(f"  Максимальное время: {report['encoding_performance']['max_time_ms']:.1f}мс")
        
        print(f"\nРазмеры кадров:")
        print(f"  Средний размер: {report['frame_sizes']['avg_size_bytes']:.0f} байт")
        print(f"  Общий объем данных: {report['frame_sizes']['total_data_mb']:.2f} МБ")
        
        print(f"\nИспользование системы:")
        print(f"  Средняя нагрузка CPU: {report['system_usage']['avg_cpu_percent']:.1f}%")
        print(f"  Максимальная нагрузка CPU: {report['system_usage']['max_cpu_percent']:.1f}%")
        print(f"  Использование памяти: {report['system_usage']['avg_memory_percent']:.1f}%")
        
        return report

def main():
    parser = argparse.ArgumentParser(description="Тест производительности видео системы")
    parser.add_argument("--test", choices=['encoding', 'realtime', 'quality', 'resolution', 'all'], 
                       default='all', help="Тип теста")
    parser.add_argument("--resolution", default="640x480", help="Разрешение (WxH)")
    parser.add_argument("--quality", type=int, default=75, help="JPEG качество")
    parser.add_argument("--fps", type=int, default=15, help="Целевой FPS")
    parser.add_argument("--duration", type=int, default=30, help="Длительность теста (сек)")
    parser.add_argument("--frames", type=int, default=100, help="Количество кадров для теста")
    
    args = parser.parse_args()
    
    # Парсинг разрешения
    try:
        width, height = map(int, args.resolution.split('x'))
        resolution = (width, height)
    except:
        print(f"Неверный формат разрешения: {args.resolution}")
        resolution = (640, 480)
    
    tester = PerformanceTest()
    
    print("ТЕСТИРОВАНИЕ ПРОИЗВОДИТЕЛЬНОСТИ ОПТИМИЗИРОВАННОЙ ВИДЕО СИСТЕМЫ")
    print("="*60)
    
    if args.test in ['encoding', 'all']:
        print("\n1. Тест кодирования JPEG")
        tester.test_jpeg_encoding(resolution, args.quality, args.frames)
        tester.generate_report()
    
    if args.test in ['realtime', 'all']:
        print("\n2. Тест кодирования в реальном времени")
        tester.test_realtime_encoding(resolution, args.quality, args.duration, args.fps)
    
    if args.test in ['quality', 'all']:
        print("\n3. Тест разных уровней качества")
        quality_results = tester.test_different_qualities(resolution)
        print("\nСводка по качеству:")
        for quality, data in quality_results.items():
            print(f"  Качество {quality}: {data['avg_encoding_time_ms']:.1f}мс, "
                  f"{data['avg_frame_size_bytes']:.0f}B, {data['max_fps']:.1f} FPS")
    
    if args.test in ['resolution', 'all']:
        print("\n4. Тест разных разрешений")
        resolution_results = tester.test_different_resolutions(args.quality)
        print("\nСводка по разрешениям:")
        for res, data in resolution_results.items():
            print(f"  {res}: {data['avg_encoding_time_ms']:.1f}мс, "
                  f"{data['avg_frame_size_bytes']:.0f}B, {data['max_fps']:.1f} FPS")
    
    print("\n" + "="*60)
    print("РЕКОМЕНДАЦИИ:")
    print("- Для низкой задержки: качество 60, разрешение 480x360, FPS 20")
    print("- Для баланса: качество 75, разрешение 640x480, FPS 15")  
    print("- Для высокого качества: качество 85, разрешение 800x600, FPS 10")
    print("- Для экономии ресурсов: качество 50, разрешение 320x240, FPS 10")

if __name__ == "__main__":
    main() 