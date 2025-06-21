#!/usr/bin/env python3
"""
Оптимизированный клиент робота для передачи видео без WebRTC
Использует MJPEG стриминг через WebSocket для лучшей производительности
"""

import asyncio
import websockets
import json
import logging
import cv2
import numpy as np
import argparse
import time
import base64
from threading import Thread, Event
import queue
from concurrent.futures import ThreadPoolExecutor
import gc

# Настройка логирования
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("robot_video")

class OptimizedVideoStreamer:
    """Оптимизированный видео стример без WebRTC"""
    
    def __init__(self, camera_index=0, test_mode=False, quality=75, fps=15, resolution=(640, 480)):
        self.camera_index = camera_index
        self.test_mode = test_mode
        self.quality = quality  # JPEG качество (0-100)
        self.fps = fps         # Целевой FPS
        self.resolution = resolution
        
        self.camera = None
        self.frame_count = 0
        self.is_streaming = False
        self.frame_queue = queue.Queue(maxsize=2)  # Небольшая очередь
        self.stop_event = Event()
        
        # Пул потоков для обработки
        self.executor = ThreadPoolExecutor(max_workers=2)
        
        # Статистика
        self.last_fps_time = time.time()
        self.fps_counter = 0
        self.actual_fps = 0
        
        self._initialize_camera()
    
    def _initialize_camera(self):
        """Инициализация камеры"""
        if self.test_mode:
            logger.info("Запуск в тестовом режиме")
            return
            
        for attempt in range(3):
            try:
                self.camera = cv2.VideoCapture(self.camera_index)
                if self.camera.isOpened():
                    # Настройки камеры
                    self.camera.set(cv2.CAP_PROP_FRAME_WIDTH, self.resolution[0])
                    self.camera.set(cv2.CAP_PROP_FRAME_HEIGHT, self.resolution[1])
                    self.camera.set(cv2.CAP_PROP_FPS, self.fps)
                    self.camera.set(cv2.CAP_PROP_BUFFERSIZE, 1)
                    
                    logger.info(f"Камера {self.camera_index} инициализирована")
                    return
                else:
                    if self.camera:
                        self.camera.release()
                        self.camera = None
                        
            except Exception as e:
                logger.error(f"Ошибка камеры: {e}")
                if self.camera:
                    self.camera.release()
                    self.camera = None
                    
            if attempt < 2:
                time.sleep(1)
        
        logger.warning("Переход в тестовый режим")
        self.test_mode = True
        self.camera = None
    
    def _create_test_frame(self):
        """Создание тестового кадра"""
        frame = np.zeros((self.resolution[1], self.resolution[0], 3), np.uint8)
        
        # Градиент
        for y in range(self.resolution[1]):
            for x in range(self.resolution[0]):
                frame[y, x] = [
                    int(255 * x / self.resolution[0]),
                    int(255 * y / self.resolution[1]),
                    int(255 * ((x + y + self.frame_count) % 256) / 256)
                ]
        
        # Текст
        cv2.putText(frame, "TEST MODE", (50, 50), cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
        cv2.putText(frame, f"Frame: {self.frame_count}", (50, 100), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
        cv2.putText(frame, f"FPS: {self.actual_fps:.1f}", (50, 130), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)
        
        return frame
    
    def _capture_frame(self):
        """Захват кадра"""
        if self.test_mode or not self.camera or not self.camera.isOpened():
            return self._create_test_frame()
        
        ret, frame = self.camera.read()
        if not ret:
            return self._create_test_frame()
        
        return frame
    
    def _encode_frame(self, frame):
        """Кодирование в JPEG"""
        try:
            encode_params = [cv2.IMWRITE_JPEG_QUALITY, self.quality]
            success, buffer = cv2.imencode('.jpg', frame, encode_params)
            
            if success:
                return base64.b64encode(buffer).decode('utf-8')
            else:
                return None
                
        except Exception as e:
            logger.error(f"Ошибка кодирования: {e}")
            return None
    
    def _frame_capture_loop(self):
        """Цикл захвата кадров"""
        frame_interval = 1.0 / self.fps
        last_frame_time = 0
        
        while not self.stop_event.is_set() and self.is_streaming:
            current_time = time.time()
            
            if current_time - last_frame_time >= frame_interval:
                try:
                    frame = self._capture_frame()
                    encoded_frame = self._encode_frame(frame)
                    
                    if encoded_frame:
                        try:
                            self.frame_queue.put_nowait({
                                'type': 'video_frame',
                                'data': encoded_frame,
                                'timestamp': current_time,
                                'frame_number': self.frame_count
                            })
                            
                            self.frame_count += 1
                            self.fps_counter += 1
                            
                        except queue.Full:
                            pass  # Пропускаем кадр
                    
                    last_frame_time = current_time
                    
                    # Подсчет FPS
                    if current_time - self.last_fps_time >= 1.0:
                        self.actual_fps = self.fps_counter / (current_time - self.last_fps_time)
                        self.fps_counter = 0
                        self.last_fps_time = current_time
                        
                except Exception as e:
                    logger.error(f"Ошибка захвата: {e}")
                    time.sleep(0.1)
            else:
                time.sleep(0.001)
    
    def start_streaming(self):
        """Запуск стриминга"""
        if self.is_streaming:
            return
            
        self.is_streaming = True
        self.stop_event.clear()
        
        # Очистка очереди
        while not self.frame_queue.empty():
            try:
                self.frame_queue.get_nowait()
            except queue.Empty:
                break
        
        self.capture_thread = Thread(target=self._frame_capture_loop, daemon=True)
        self.capture_thread.start()
        
        logger.info("Видео стриминг запущен")
    
    def stop_streaming(self):
        """Остановка стриминга"""
        if not self.is_streaming:
            return
            
        self.is_streaming = False
        self.stop_event.set()
        
        if hasattr(self, 'capture_thread') and self.capture_thread.is_alive():
            self.capture_thread.join(timeout=1.0)
        
        # Очистка очереди
        while not self.frame_queue.empty():
            try:
                self.frame_queue.get_nowait()
            except queue.Empty:
                break
                
        logger.info("Видео стриминг остановлен")
    
    def get_frame(self):
        """Получение кадра"""
        try:
            return self.frame_queue.get_nowait()
        except queue.Empty:
            return None
    
    def cleanup(self):
        """Очистка ресурсов"""
        self.stop_streaming()
        
        if self.camera:
            self.camera.release()
            self.camera = None
            
        self.executor.shutdown(wait=True)
        gc.collect()

class OptimizedRobotClient:
    """Оптимизированный робот клиент"""
    
    def __init__(self, server_url, motor_controller=None, video_config=None):
        self.server_url = server_url
        self.motor_controller = motor_controller
        self.socket = None
        
        # Видео конфигурация
        video_config = video_config or {}
        self.video_quality = video_config.get('quality', 75)
        self.video_fps = video_config.get('fps', 15)
        self.video_resolution = video_config.get('resolution', (640, 480))
        self.test_video_mode = video_config.get('test_mode', False)
        self.camera_index = video_config.get('camera_index', 0)
        
        self.video_streamer = None
        
        # Моторы
        self.last_command_time = time.time()
        self.command_timeout = 2.0
        self.motor_disable_timeout = 60.0
        self.motors_stopped = False
        self.motors_disabled = False
        self.is_connection_active = False
        
        logger.info(f"Клиент инициализирован: {server_url}")
    
    async def connect(self):
        """Подключение к серверу"""
        logger.info("Подключение к серверу...")
        
        try:
            self.is_connection_active = True
            safety_task = asyncio.create_task(self._safety_check_loop())
            
            async with websockets.connect(self.server_url) as socket:
                self.socket = socket
                logger.info("Подключение установлено")
                
                self._initialize_video_streamer()
                
                await self.socket.send("REGISTER!ROBOT")
                
                # Основной цикл
                while True:
                    try:
                        message = await asyncio.wait_for(self.socket.recv(), timeout=0.1)
                        await self._handle_message(message)
                        
                    except asyncio.TimeoutError:
                        await self._send_video_frame()
                        
                    except websockets.exceptions.ConnectionClosed:
                        logger.warning("Соединение закрыто")
                        break
                        
                    except Exception as e:
                        logger.error(f"Ошибка обработки: {e}")
                        
        except Exception as e:
            logger.error(f"Ошибка подключения: {e}")
        finally:
            self.is_connection_active = False
            safety_task.cancel()
            try:
                await safety_task
            except asyncio.CancelledError:
                pass
            await self._cleanup()
    
    def _initialize_video_streamer(self):
        """Инициализация видео стримера"""
        try:
            self.video_streamer = OptimizedVideoStreamer(
                camera_index=self.camera_index,
                test_mode=self.test_video_mode,
                quality=self.video_quality,
                fps=self.video_fps,
                resolution=self.video_resolution
            )
            logger.info("Видео стример инициализирован")
        except Exception as e:
            logger.error(f"Ошибка видео стримера: {e}")
    
    async def _send_video_frame(self):
        """Отправка видео кадра"""
        if not self.video_streamer or not self.video_streamer.is_streaming:
            return
            
        frame_data = self.video_streamer.get_frame()
        if frame_data and self.socket:
            try:
                message = f"VIDEO_FRAME!{json.dumps(frame_data)}"
                await self.socket.send(message)
            except Exception as e:
                logger.error(f"Ошибка отправки кадра: {e}")
    
    async def _handle_message(self, message):
        """Обработка сообщений"""
        try:
            if not message.strip():
                return
                
            if message.startswith("COMMAND!"):
                data = json.loads(message[8:])
                self.last_command_time = time.time()
                await self._handle_command(data)
                return
            
            if message == "REQUEST_VIDEO_STREAM":
                await self._start_video_streaming()
                return
            elif message == "STOP_VIDEO_STREAM":
                await self._stop_video_streaming()
                return
            
            if message.startswith("REGISTERED!"):
                logger.info("Зарегистрирован")
                return
                
        except Exception as e:
            logger.error(f"Ошибка обработки сообщения: {e}")
    
    async def _start_video_streaming(self):
        """Запуск видео"""
        if self.video_streamer:
            self.video_streamer.start_streaming()
    
    async def _stop_video_streaming(self):
        """Остановка видео"""
        if self.video_streamer:
            self.video_streamer.stop_streaming()
    
    async def _handle_command(self, data):
        """Обработка команд"""
        try:
            if "leftStickValue" in data:
                left_stick = data.get("leftStickValue", {"x": 0, "y": 0})
                
                if self.motor_controller and not self.motors_disabled:
                    left_speed, right_speed = self._calculate_motor_speeds(
                        left_stick["x"], left_stick["y"]
                    )
                    self.motor_controller.set_motors(left_speed, right_speed)
                
                await self._send_telemetry({"leftStick": left_stick})
                
        except Exception as e:
            logger.error(f"Ошибка команды: {e}")
    
    def _calculate_motor_speeds(self, x, y):
        """Расчет скоростей моторов"""
        left_speed = max(-1, min(1, y + x))
        right_speed = max(-1, min(1, y - x))
        return left_speed, right_speed
    
    async def _send_telemetry(self, data):
        """Отправка телеметрии"""
        try:
            message = json.dumps({"type": "telemetry", "data": data})
            await self.socket.send(message)
        except Exception as e:
            logger.error(f"Ошибка телеметрии: {e}")
    
    async def _safety_check_loop(self):
        """Система безопасности моторов"""
        while self.is_connection_active:
            try:
                current_time = time.time()
                elapsed_time = current_time - self.last_command_time
                
                if elapsed_time > self.motor_disable_timeout:
                    if not self.motors_disabled:
                        await self._disable_motors()
                        self.motors_disabled = True
                        self.motors_stopped = True
                elif elapsed_time > self.command_timeout:
                    if not self.motors_stopped:
                        self.motors_stopped = True
                    if not self.motors_disabled:
                        await self._stop_motors()
                else:
                    self.motors_stopped = False
                    self.motors_disabled = False
                
                await asyncio.sleep(0.5)
                
            except Exception as e:
                logger.error(f"Ошибка безопасности: {e}")
                await self._stop_motors()
    
    async def _stop_motors(self):
        """Остановка моторов"""
        if self.motor_controller and not self.motors_disabled:
            self.motor_controller.set_motors(0, 0)
    
    async def _disable_motors(self):
        """Отключение моторов"""
        if self.motor_controller:
            self.motor_controller.set_motors(0, 0)
    
    async def _cleanup(self):
        """Очистка ресурсов"""
        if self.video_streamer:
            self.video_streamer.cleanup()
        
        if self.motor_controller:
            await self._stop_motors()

async def main():
    parser = argparse.ArgumentParser(description="Оптимизированный клиент робота")
    parser.add_argument("--server", default="ws://193.169.240.11:8080")
    parser.add_argument("--camera", type=int, default=0)
    parser.add_argument("--quality", type=int, default=75)
    parser.add_argument("--fps", type=int, default=15)
    parser.add_argument("--resolution", default="640x480")
    parser.add_argument("--use-motors", action="store_true")
    parser.add_argument("--test-video", action="store_true")
    parser.add_argument("--debug", action="store_true")
    
    args = parser.parse_args()
    
    if args.debug:
        logger.setLevel(logging.DEBUG)
    
    # Парсинг разрешения
    try:
        width, height = map(int, args.resolution.split('x'))
        resolution = (width, height)
    except:
        resolution = (640, 480)
    
    # Инициализация контроллера моторов
    motor_controller = None
    if args.use_motors:
        try:
            from motor_controller import MotorController
            motor_controller = MotorController()
        except Exception as e:
            logger.error(f"Ошибка моторов: {e}")
    
    # Конфигурация видео
    video_config = {
        'camera_index': args.camera,
        'quality': args.quality,
        'fps': args.fps,
        'resolution': resolution,
        'test_mode': args.test_video
    }
    
    # Запуск клиента
    robot = OptimizedRobotClient(args.server, motor_controller, video_config)
    
    try:
        await robot.connect()
    finally:
        if motor_controller:
            motor_controller.cleanup()

if __name__ == "__main__":
    asyncio.run(main()) 