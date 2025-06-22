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
logging.basicConfig(level=logging.DEBUG, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger("robot_video")

class OptimizedVideoStreamer:
    """Оптимизированный видео стример без WebRTC"""
    
    def __init__(self, camera_index=0, test_mode=False, quality=60, fps=30, resolution=(480, 360)):
        self.camera_index = camera_index
        self.test_mode = test_mode
        self.quality = quality  # JPEG качество (0-100)
        self.fps = fps         # Целевой FPS
        self.resolution = resolution
        
        self.camera = None
        self.frame_count = 0
        self.is_streaming = False
        self.frame_queue = queue.Queue(maxsize=1)  # Минимальная очередь для низкой задержки
        self.stop_event = Event()
        
        # Пул потоков для обработки
        self.executor = ThreadPoolExecutor(max_workers=2)
        
        # Статистика
        self.last_fps_time = time.time()
        self.fps_counter = 0
        self.actual_fps = 0
        
        self._initialize_camera()
    
    def _test_camera_read_with_timeout(self, timeout=3.0):
        """Тестирование чтения кадра с таймаутом"""
        def read_frame():
            try:
                ret, frame = self.camera.read()
                return ret, frame
            except Exception as e:
                logger.error(f"Ошибка чтения кадра: {e}")
                return False, None
        
        try:
            future = self.executor.submit(read_frame)
            ret, frame = future.result(timeout=timeout)
            return ret, frame
        except Exception as e:
            logger.warning(f"⏰ Таймаут тестирования камеры ({timeout}s): {e}")
            return False, None
    
    def _initialize_camera(self):
        """Инициализация камеры"""
        # ВРЕМЕННОЕ РЕШЕНИЕ: Принудительный тестовый режим для отладки зависания (ОТКЛЮЧЕН)
        # logger.info("🧪 ПРИНУДИТЕЛЬНЫЙ ТЕСТОВЫЙ РЕЖИМ (для отладки зависания камеры)")
        # self.test_mode = True
        # return
        
        if self.test_mode:
            logger.info("Запуск в тестовом режиме")
            return
            
        logger.info(f"Попытка инициализации камеры {self.camera_index}...")
        
        for attempt in range(3):
            try:
                logger.info(f"Попытка {attempt + 1}/3 инициализации камеры")
                
                # Создаем камеру с таймаутом
                self.camera = cv2.VideoCapture(self.camera_index)
                
                # Проверяем открытие с таймаутом
                if self.camera.isOpened():
                    # Настройки камеры для минимальной задержки
                    self.camera.set(cv2.CAP_PROP_FRAME_WIDTH, self.resolution[0])
                    self.camera.set(cv2.CAP_PROP_FRAME_HEIGHT, self.resolution[1])
                    self.camera.set(cv2.CAP_PROP_FPS, self.fps)
                    self.camera.set(cv2.CAP_PROP_BUFFERSIZE, 1)  # Минимальный буфер
                    self.camera.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc('M', 'J', 'P', 'G'))  # MJPEG для скорости
                    
                    # Проверяем чтение кадра с таймаутом
                    logger.info("🧪 Тестируем чтение кадра (с таймаутом 3с)...")
                    ret, test_frame = self._test_camera_read_with_timeout(timeout=2.0)  # Быстрее инициализация
                    logger.info(f"🧪 Тест чтения завершен: ret={ret}")
                    
                    if ret and test_frame is not None:
                        logger.info(f"✅ Камера {self.camera_index} успешно инициализирована")
                        logger.info(f"Разрешение: {test_frame.shape[1]}x{test_frame.shape[0]}")
                        return
                    else:
                        logger.warning(f"⚠️ Камера открылась, но не может читать кадры")
                        
                if self.camera:
                    self.camera.release()
                    self.camera = None
                        
            except Exception as e:
                logger.error(f"❌ Ошибка инициализации камеры (попытка {attempt + 1}): {e}")
                if self.camera:
                    try:
                        self.camera.release()
                    except:
                        pass
                    self.camera = None
                    
            if attempt < 2:
                logger.info(f"Пауза перед следующей попыткой...")
                time.sleep(2)
        
        logger.warning("❌ Не удалось инициализировать камеру. Переход в тестовый режим")
        self.test_mode = True
        self.camera = None
        
        # ВРЕМЕННОЕ РЕШЕНИЕ: Принудительно включаем тестовый режим (ОТКЛЮЧЕН)
        # logger.info("🧪 ПРИНУДИТЕЛЬНЫЙ ТЕСТОВЫЙ РЕЖИМ для отладки")
        # self.test_mode = True
        # if self.camera:
        #     try:
        #         self.camera.release()
        #     except:
        #         pass
        #     self.camera = None
    
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
        
        try:
            logger.debug("📷 Вызываем camera.read() с таймаутом...")
            ret, frame = self._test_camera_read_with_timeout(timeout=0.5)  # Быстрее захват кадров
            logger.debug(f"📷 camera.read() завершен: ret={ret}, frame={'OK' if frame is not None else 'None'}")
            
            if not ret or frame is None:
                logger.debug("Не удалось прочитать кадр с камеры, используем тестовый")
                return self._create_test_frame()
            
            return frame
            
        except Exception as e:
            logger.error(f"Ошибка захвата кадра: {e}")
            return self._create_test_frame()
    
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
        logger.info("🎥 Поток захвата кадров запущен")
        
        frame_interval = 1.0 / self.fps
        last_frame_time = 0
        capture_count = 0
        
        while not self.stop_event.is_set() and self.is_streaming:
            current_time = time.time()
            
            if current_time - last_frame_time >= frame_interval:
                try:
                    capture_count += 1
                    
                    # Логируем каждые 30 кадров (и первые 3 кадра для отладки)
                    if capture_count % 30 == 1 or capture_count <= 3:
                        logger.info(f"🎬 Захват кадров: #{capture_count}, FPS: {self.actual_fps:.1f}")
                    
                    logger.debug(f"🎥 Захватываем кадр #{capture_count}...")
                    frame = self._capture_frame()
                    logger.debug(f"🎥 Кадр #{capture_count} захвачен: {frame is not None}")
                    if frame is None:
                        logger.warning("⚠️ Не удалось захватить кадр")
                        continue
                        
                    logger.debug(f"🎨 Кодируем кадр #{capture_count}...")
                    encoded_frame = self._encode_frame(frame)
                    logger.debug(f"🎨 Кадр #{capture_count} закодирован: {encoded_frame is not None}")
                    
                    if encoded_frame:
                        try:
                            frame_data = {
                                'type': 'video_frame',
                                'data': encoded_frame,
                                'timestamp': current_time,
                                'frame_number': self.frame_count
                            }
                            
                            # Очищаем очередь для минимальной задержки
                            try:
                                self.frame_queue.get_nowait()  # Удаляем старый кадр
                            except queue.Empty:
                                pass
                            
                            self.frame_queue.put_nowait(frame_data)
                            
                            self.frame_count += 1
                            self.fps_counter += 1
                            
                            logger.debug(f"🎬 Кадр #{self.frame_count} добавлен в очередь")
                            
                        except queue.Full:
                            logger.debug("📦 Очередь кадров полная, пропускаем кадр")
                    else:
                        logger.warning("⚠️ Не удалось закодировать кадр")
                    
                    last_frame_time = current_time
                    
                    # Подсчет FPS
                    if current_time - self.last_fps_time >= 1.0:
                        self.actual_fps = self.fps_counter / (current_time - self.last_fps_time)
                        self.fps_counter = 0
                        self.last_fps_time = current_time
                        
                except Exception as e:
                    logger.error(f"❌ Ошибка захвата кадра #{capture_count}: {e}")
                    time.sleep(0.1)
            else:
                time.sleep(0.0001)  # Уменьшаем задержку для более быстрой реакции
        
        logger.info("🛑 Поток захвата кадров завершен")
    
    def start_streaming(self):
        """Запуск стриминга"""
        if self.is_streaming:
            logger.info("Видео стриминг уже запущен")
            return
            
        logger.info("🎬 Запуск видео стриминга...")
        self.is_streaming = True
        self.stop_event.clear()
        
        # Очистка очереди
        queue_size = self.frame_queue.qsize()
        if queue_size > 0:
            logger.info(f"🗑️ Очистка очереди кадров ({queue_size} кадров)")
            
        while not self.frame_queue.empty():
            try:
                self.frame_queue.get_nowait()
            except queue.Empty:
                break
        
        # Запуск потока захвата
        logger.info("🎥 Запуск потока захвата кадров...")
        self.capture_thread = Thread(target=self._frame_capture_loop, daemon=True)
        self.capture_thread.start()
        
        # Проверяем, что поток запустился
        logger.info("⏳ Ожидание запуска потока захвата...")
        time.sleep(0.01)  # Уменьшаем задержку запуска
        logger.info("⏳ Проверка состояния потока...")
        
        if self.capture_thread.is_alive():
            logger.info("✅ Поток захвата кадров успешно запущен")
        else:
            logger.error("❌ Не удалось запустить поток захвата кадров")
        
        logger.info("📋 Завершение функции start_streaming...")
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
            frame_data = self.frame_queue.get_nowait()
            logger.debug(f"🎬 Получен кадр из очереди: #{frame_data.get('frame_number', '?')}")
            return frame_data
        except queue.Empty:
            logger.debug("📭 Очередь кадров пуста")
            return None
        except Exception as e:
            logger.error(f"❌ Ошибка получения кадра: {e}")
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
        self.video_quality = video_config.get('quality', 60)  # Понижаем качество для скорости
        self.video_fps = video_config.get('fps', 30)  # Повышаем FPS для плавности
        self.video_resolution = video_config.get('resolution', (480, 360))  # Уменьшаем разрешение для скорости
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
                logger.info("Зарегистрирован как ROBOT")
                
                # Запускаем видео стриминг автоматически
                await self._start_video_streaming()
                logger.info("Видео стриминг запущен автоматически")
                
                # Запускаем задачу отправки видео
                logger.info("🚀 Создаем задачу отправки видео...")
                try:
                    video_task = asyncio.create_task(self._video_send_loop())
                    logger.info("✅ Задача отправки видео создана")
                except Exception as e:
                    logger.error(f"❌ Ошибка создания задачи видео: {e}")
                    import traceback
                    logger.error(f"Трассировка: {traceback.format_exc()}")
                    raise
                
                # Даем время задаче запуститься
                logger.info("⏳ Ожидание запуска задачи видео (sleep 0.01)...")
                await asyncio.sleep(0.01)  # Минимальная задержка
                logger.info("⏳ Проверка состояния задачи видео...")
                
                # Проверяем состояние задачи
                if video_task.done():
                    logger.error("❌ Задача видео завершилась преждевременно!")
                    try:
                        result = video_task.result()
                        logger.info(f"Результат задачи: {result}")
                    except Exception as e:
                        logger.error(f"❌ Исключение в задаче видео: {e}")
                        import traceback
                        logger.error(f"Трассировка: {traceback.format_exc()}")
                else:
                    logger.info("✅ Задача видео работает")
                
                # Основной цикл обработки сообщений
                logger.info("🔄 Запуск основного цикла обработки сообщений")
                message_count = 0
                
                timeout_count = 0
                
                while True:
                    try:
                        logger.debug(f"🎧 Ожидание сообщения (таймаут #{timeout_count})...")
                        message = await asyncio.wait_for(self.socket.recv(), timeout=0.1)  # Быстрее реакция на сообщения
                        message_count += 1
                        timeout_count = 0  # Сбрасываем счетчик таймаутов
                        
                        logger.debug(f"📨 Получено сообщение #{message_count}: {message[:50]}...")
                        
                        if message_count % 100 == 0:
                            logger.info(f"📨 Обработано сообщений: {message_count}")
                        
                        await self._handle_message(message)
                        
                    except asyncio.TimeoutError:
                        timeout_count += 1
                        if timeout_count % 50 == 0:  # Каждые 5 секунд (50 * 0.1s)
                            logger.info(f"⏰ Таймаут ожидания сообщений: {timeout_count/10:.1f} секунд")
                        # Это нормально - просто продолжаем цикл
                        continue
                        
                    except websockets.exceptions.ConnectionClosed:
                        logger.warning("🔌 Соединение закрыто сервером")
                        break
                        
                    except Exception as e:
                        logger.error(f"❌ Ошибка в основном цикле: {e}")
                        break
                        
        except Exception as e:
            logger.error(f"Ошибка подключения: {e}")
        finally:
            self.is_connection_active = False
            
            # Останавливаем все задачи
            safety_task.cancel()
            if 'video_task' in locals():
                video_task.cancel()
            
            try:
                await safety_task
            except asyncio.CancelledError:
                pass
                
            try:
                if 'video_task' in locals():
                    await video_task
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
    
    async def _video_send_loop(self):
        """Отдельный цикл для отправки видео"""
        logger.info("🎬 ВХОД в функцию _video_send_loop")
        
        try:
            logger.info("Запуск цикла отправки видео")
            
            frame_send_timeout = 2.0  # Таймаут отправки кадра
            last_successful_send = time.time()
            consecutive_errors = 0
            max_consecutive_errors = 10
            loop_count = 0
            
            logger.info("🔄 Входим в цикл отправки видео...")
            logger.info(f"🔗 Состояние подключения: {self.is_connection_active}")
            
            while self.is_connection_active:
                try:
                    loop_count += 1
                    
                    # Логируем каждые 30 итераций (примерно каждые 2 секунды)
                    if loop_count % 30 == 1:
                        logger.info(f"🔄 Цикл видео: итерация {loop_count}, ошибок подряд: {consecutive_errors}")
                    
                    # Проверяем таймаут отправки
                    current_time = time.time()
                    if current_time - last_successful_send > frame_send_timeout:
                        logger.warning(f"⚠️ Таймаут отправки видео: {current_time - last_successful_send:.1f}s")
                        last_successful_send = current_time
                    
                    # Отправляем кадр
                    success = await self._send_video_frame()
                    if success:
                        last_successful_send = current_time
                        consecutive_errors = 0
                    else:
                        consecutive_errors += 1
                    
                    # Проверяем количество ошибок подряд
                    if consecutive_errors >= max_consecutive_errors:
                        logger.error(f"❌ Слишком много ошибок отправки видео подряд: {consecutive_errors}")
                        await asyncio.sleep(1.0)  # Уменьшаем паузу при ошибках
                        consecutive_errors = 0
                    
                    await asyncio.sleep(1/30)  # 30 FPS для более плавного видео
                    
                except Exception as e:
                    logger.error(f"❌ Ошибка в цикле видео (итерация {loop_count}): {e}")
                    consecutive_errors += 1
                    await asyncio.sleep(0.1)  # Быстрее восстановление после ошибок
            
        except Exception as e:
            logger.error(f"❌ КРИТИЧЕСКАЯ ошибка в _video_send_loop: {e}")
            import traceback
            logger.error(f"Трассировка: {traceback.format_exc()}")
        
        logger.info("🛑 Цикл отправки видео завершен")
    
    async def _send_video_frame(self):
        """Отправка видео кадра"""
        try:
            # Проверяем видео стример
            if not self.video_streamer:
                logger.debug("Видео стример не инициализирован")
                return False
                
            if not self.video_streamer.is_streaming:
                logger.debug("Видео стриминг не активен")
                return False
            
            # Получаем кадр
            frame_data = self.video_streamer.get_frame()
            if not frame_data:
                logger.debug("Нет кадров для отправки")
                return False
                
            # Проверяем сокет
            if not self.socket:
                logger.debug("WebSocket не подключен")
                return False
            
            # Подготавливаем сообщение
            try:
                message = f"VIDEO_FRAME!{json.dumps(frame_data)}"
                message_size = len(message)
                
                # Отправляем с минимальным таймаутом
                await asyncio.wait_for(self.socket.send(message), timeout=0.5)
                
                logger.debug(f"📤 Отправлен кадр #{frame_data.get('frame_number', '?')} ({message_size} байт)")
                return True
                
            except json.JSONEncodeError as e:
                logger.error(f"❌ Ошибка JSON кодирования: {e}")
                return False
            
        except asyncio.TimeoutError:
            logger.warning("⏰ Таймаут отправки кадра")
            return False
        except Exception as e:
            logger.error(f"❌ Ошибка отправки кадра: {e}")
            return False
    
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
            logger.info("🎥 Видео стриминг ЗАПУЩЕН")
        else:
            logger.error("❌ Видео стример не найден!")
    
    async def _stop_video_streaming(self):
        """Остановка видео"""
        if self.video_streamer:
            self.video_streamer.stop_streaming()
            logger.info("🛑 Видео стриминг ОСТАНОВЛЕН")
        else:
            logger.error("❌ Видео стример не найден!")
    
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
                
                await asyncio.sleep(0.1)  # Быстрее проверки безопасности
                
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