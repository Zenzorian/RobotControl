import asyncio
import websockets
import json
import logging
import cv2
import numpy as np
import argparse
import time
from aiortc import RTCPeerConnection, RTCSessionDescription, VideoStreamTrack
from aiortc.contrib.media import MediaPlayer, MediaRelay
from aiortc.mediastreams import VideoFrame

# Налаштування логування
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("robot")

class CameraVideoStreamTrack(VideoStreamTrack):
    def __init__(self, camera_index=0):
        super().__init__()
        self.camera = cv2.VideoCapture(camera_index)
        self.camera.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.camera.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        
    async def recv(self):
        pts, time_base = await self.next_timestamp()
        ret, frame = self.camera.read()
        if not ret:
            logger.warning("Не вдалося отримати кадр з камери")
            # Створюємо порожній кадр якщо читання не вдалося
            frame = np.zeros((480, 640, 3), np.uint8)
        
        # Підготовлюємо кадр для відправлення через WebRTC
        frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
        frame = VideoFrame.from_ndarray(frame, format="rgb24")
        frame.pts = pts
        frame.time_base = time_base
        return frame

class RobotClient:
    def __init__(self, server_url, motor_controller=None):
        self.server_url = server_url
        self.motor_controller = motor_controller
        self.pc = None
        self.socket = None
        self.track = None
        self.camera_player = None
        self.camera_relay = MediaRelay()
        self.last_command_time = time.time()
        self.command_timeout = 2.0  # Таймаут у секундах
        self.safety_check_interval = 0.5  # Інтервал перевірки у секундах
        self.is_connection_active = False
        logger.debug(f"Ініціалізовано RobotClient для сервера {server_url}")
        
    async def connect(self):
        """Підключитися до WebSocket сервера і почати цикл обробки повідомлень"""
        logger.info(f"Підключення до сервера {self.server_url}")
        
        try:
            self.is_connection_active = True
            # Запускаємо задачу періодичної перевірки зв'язку
            safety_task = asyncio.create_task(self._safety_check_loop())
            
            async with websockets.connect(self.server_url) as socket:
                self.socket = socket
                logger.info("Підключення встановлено")
                
                # Ініціалізуємо захоплення з камери
                self._setup_camera()
                
                # Відправляємо повідомлення про підключення
                await self._send_message("connect", {"role": "robot"})
                logger.debug("Відправлено повідомлення про підключення")
                
                # Нескінченний цикл обробки повідомлень
                while True:
                    try:
                        message = await self.socket.recv()
                        logger.debug(f"Отримано повідомлення: {message[:100]}..." if len(message) > 100 else f"Отримано повідомлення: {message}")
                        await self._handle_message(message)
                    except websockets.exceptions.ConnectionClosed:
                        logger.warning("З'єднання з сервером закрито")
                        self.is_connection_active = False
                        await self._stop_motors()
                        break
                    except Exception as e:
                        logger.error(f"Помилка при обробці повідомлення: {e}")
                        
        except Exception as e:
            logger.error(f"Помилка підключення до сервера: {e}")
            self.is_connection_active = False
            await self._stop_motors()
        finally:
            logger.info("Закриття з'єднання")
            self.is_connection_active = False
            safety_task.cancel()
            try:
                await safety_task
            except asyncio.CancelledError:
                pass
            await self._cleanup()
    
    async def _safety_check_loop(self):
        """Періодична перевірка зв'язку та актуальності команд"""
        logger.info("Запуск контуру безпеки")
        try:
            while self.is_connection_active:
                current_time = time.time()
                elapsed_time = current_time - self.last_command_time
                
                if elapsed_time > self.command_timeout:
                    logger.warning(f"Таймаут команд ({elapsed_time:.1f}с). Зупиняємо мотори.")
                    await self._stop_motors()
                
                await asyncio.sleep(self.safety_check_interval)
        except asyncio.CancelledError:
            logger.debug("Завершення контуру безпеки")
        except Exception as e:
            logger.error(f"Помилка в контурі безпеки: {e}")
            await self._stop_motors()
    
    async def _stop_motors(self):
        """Зупинка моторів"""
        if self.motor_controller:
            logger.info("Аварійна зупинка моторів")
            self.motor_controller.set_motors(0, 0)
    
    async def _handle_message(self, message):
        """Обробка вхідного повідомлення від сервера"""
        try:
            # Перевіряємо формат COMMAND!{json}
            if message.startswith("COMMAND!"):
                json_data = message[8:]  # Відрізаємо COMMAND!
                data = json.loads(json_data)
                self.last_command_time = time.time()  # Оновлюємо час останньої команди
                await self._handle_command(data)
                return
                
            # Стандартна обробка JSON повідомлень
            data = json.loads(message)
            message_type = data.get("type")
            logger.debug(f"Обробка повідомлення типу {message_type}")
            
            if message_type == "offer":
                await self._handle_offer(data)
            elif message_type == "command":
                self.last_command_time = time.time()  # Оновлюємо час останньої команди
                await self._handle_command(data)
            else:
                logger.warning(f"Невідомий тип повідомлення: {message_type}")
        except json.JSONDecodeError:
            logger.error("Неможливо декодувати JSON повідомлення")
        except Exception as e:
            logger.error(f"Помилка при обробці повідомлення: {e}")
    
    async def _handle_command(self, data):
        """Обробка команди керування від сервера"""
        try:
            # Обробка прямого формату команди від Unity контролера
            if "leftStickValue" in data:
                left_stick = data.get("leftStickValue", {"x": 0, "y": 0})
                right_stick = data.get("rightStickValue", {"x": 0, "y": 0})
                camera_angle = data.get("cameraAngle", 90.0)
                
                logger.debug(f"Джойстики: лівий={left_stick}, правий={right_stick}, кут камери={camera_angle}")
                
                # Керування моторами на основі лівого джойстика
                if self.motor_controller:
                    # Використовуємо значення x та y лівого джойстика для обчислення швидкостей моторів
                    left_speed, right_speed = self._calculate_motor_speeds(left_stick["x"], left_stick["y"])
                    logger.debug(f"Встановлення швидкостей моторів: лівий={left_speed}, правий={right_speed}")
                    self.motor_controller.set_motors(left_speed, right_speed)
                
                # Відправляємо телеметрію
                await self._send_telemetry({
                    "leftStick": left_stick, 
                    "rightStick": right_stick, 
                    "cameraAngle": camera_angle
                })
                return
            
            # Обробка старого формату команди (для зворотної сумісності)
            command = data.get("command", {})
            command_type = command.get("type")
            logger.debug(f"Отримана команда: {command_type}")
            
            if command_type == "joystick":
                x = command.get("x", 0)
                y = command.get("y", 0)
                logger.debug(f"Джойстик: x={x}, y={y}")
                
                # Керування моторами
                if self.motor_controller:
                    left_speed, right_speed = self._calculate_motor_speeds(x, y)
                    logger.debug(f"Встановлення швидкостей моторів: лівий={left_speed}, правий={right_speed}")
                    self.motor_controller.set_motors(left_speed, right_speed)
                
                # Відправляємо телеметрію
                await self._send_telemetry({"x": x, "y": y})
                
        except Exception as e:
            logger.error(f"Помилка обробки команди: {e}")
    
    def _calculate_motor_speeds(self, x, y):
        """Розрахунок швидкостей моторів на основі координат джойстика"""
        # Припускаємо, що x та y мають значення від -1 до 1
        # Проста диференціальна схема керування для руху
        left_speed = y + x
        right_speed = y - x
        
        # Обмежуємо швидкості в діапазоні від -1 до 1
        left_speed = max(-1, min(1, left_speed))
        right_speed = max(-1, min(1, right_speed))
        
        return left_speed, right_speed
    
    async def _handle_offer(self, data):
        try:
            # Створюємо нове RTCPeerConnection для WebRTC
            self.pc = RTCPeerConnection()
            
            # Додаємо відеотрек
            self.track = CameraVideoStreamTrack()
            self.pc.addTrack(self.track)
            
            # Встановлюємо обробники подій
            @self.pc.on("icecandidate")
            async def on_icecandidate(candidate):
                if candidate:
                    candidate_json = {"candidate": candidate.candidate, 
                                     "sdpMid": candidate.sdpMid,
                                     "sdpMLineIndex": candidate.sdpMLineIndex}
                    await self.socket.send(f"CANDIDATE!{json.dumps(candidate_json)}")
            
            # Розбираємо та встановлюємо SDP offer
            offer = RTCSessionDescription(sdp=data["sdp"], type=data["type"])
            await self.pc.setRemoteDescription(offer)
            
            # Створюємо та відправляємо SDP answer
            answer = await self.pc.createAnswer()
            await self.pc.setLocalDescription(answer)
            
            answer_json = {"sdp": self.pc.localDescription.sdp,
                          "type": self.pc.localDescription.type}
            await self.socket.send(f"ANSWER!{json.dumps(answer_json)}")
        except Exception as e:
            logger.error(f"Помилка обробки WebRTC offer: {e}")
    
    async def _send_message(self, type, data):
        message = json.dumps({"type": type, "data": data})
        await self.socket.send(message)
    
    async def _send_telemetry(self, data):
        message = json.dumps({"type": "telemetry", "data": data})
        await self.socket.send(message)
    
    def _setup_camera(self):
        # Реалізація налаштування камери
        pass
    
    async def _cleanup(self):
        # Реалізація очищення ресурсів
        pass

async def main():
    parser = argparse.ArgumentParser(description="Клієнт робота для підключення до сервера керування")
    parser.add_argument("--server", type=str, default="ws://193.169.240.11:8080",
                       help="WebSocket URL сервера (за замовчуванням: ws://193.169.240.11:8080)")
    parser.add_argument("--camera", type=int, default=0,
                       help="Індекс USB-камери (за замовчуванням: 0)")
    parser.add_argument("--use-motors", action="store_true",
                       help="Увімкнути керування реальними моторами через GPIO")
    parser.add_argument("--debug", action="store_true",
                       help="Увімкнути детальне логування")
    parser.add_argument("--timeout", type=float, default=2.0,
                       help="Час очікування команд до зупинки моторів (секунди, за замовчуванням: 2.0)")
    
    args = parser.parse_args()
    
    # Налаштовуємо рівень логування
    if args.debug:
        logger.setLevel(logging.DEBUG)
        # Додаємо форматування для відображення часу та рівня логу
        handler = logging.StreamHandler()
        formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
        handler.setFormatter(formatter)
        logger.handlers = [handler]
        logger.debug("Увімкнено детальне логування")
    
    # Створюємо контролер моторів, якщо потрібно
    motor_controller = None
    if args.use_motors:
        try:
            from motor_controller import MotorController
            motor_controller = MotorController()
            logger.info("Ініціалізовано контролер моторів")
        except Exception as e:
            logger.error(f"Не вдалося ініціалізувати контролер моторів: {e}")
    
    # Створюємо та запускаємо клієнт робота
    robot = RobotClient(args.server, motor_controller)
    robot.command_timeout = args.timeout  # Встановлюємо таймаут з аргументів
    try:
        await robot.connect()
    finally:
        # Закриваємо ресурси при завершенні
        if motor_controller:
            motor_controller.cleanup()

if __name__ == "__main__":
    asyncio.run(main()) 