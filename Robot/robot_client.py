import asyncio
import websockets
import json
import logging
import time
import argparse

# Налаштування логування
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("robot")

class RobotClient:
    def __init__(self, server_url, motor_controller=None):
        self.server_url = server_url
        self.motor_controller = motor_controller
        self.socket = None
        self.last_command_time = time.time()
        self.command_timeout = 2.0   # Таймаут для команд остановки (2 секунды)
        self.motor_disable_timeout = 60.0  # Полное отключение моторов через 60 секунд
        self.motors_stopped = False  # Флаг для отслеживания состояния моторов
        self.motors_disabled = False # Флаг полного отключения управления моторами
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
                
                # Відправляємо строку реєстрації
                await self.socket.send("REGISTER!ROBOT")
                logger.debug("Відправлено REGISTER!ROBOT")
                
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
                
                # Перевіряємо, чи не застарілі команди управління
                if current_time - self.last_command_time > self.command_timeout:
                    if not self.motors_stopped:
                        logger.warning(f"Команди не отримувалися {self.command_timeout} сек. Зупинка моторів.")
                        await self._stop_motors()
                
                # Перевіряємо повне відключення моторів при довгій бездіяльності
                if current_time - self.last_command_time > self.motor_disable_timeout:
                    if not self.motors_disabled:
                        logger.warning(f"Команди не отримувалися {self.motor_disable_timeout} сек. Повне відключення моторів.")
                        await self._disable_motors()
                        
                await asyncio.sleep(self.safety_check_interval)
        except asyncio.CancelledError:
            logger.info("Контур безпеки завершено")
    
    async def _stop_motors(self):
        """Зупинити всі мотори, але залишити їх увімкненими"""
        if self.motor_controller and not self.motors_stopped:
            logger.info("🛑 Зупинка всіх моторів (залишити увімкненими)")
            await self.motor_controller.stop_all()
            self.motors_stopped = True
    
    async def _disable_motors(self):
        """Повністю відключити мотори"""
        if self.motor_controller and not self.motors_disabled:
            logger.info("🔌 Повне відключення моторів")
            await self.motor_controller.disable_all()
            self.motors_disabled = True
    
    async def _handle_message(self, message):
        """Обробити отримане повідомлення"""
        try:
            # Перевіряємо, чи це JSON повідомлення
            if message.startswith('{') and message.endswith('}'):
                data = json.loads(message)
                
                if data.get('type') == 'command':
                    await self._handle_command(data)
                elif data.get('type') == 'telemetry_request':
                    # Відправляємо телеметрію
                    telemetry = {
                        'type': 'telemetry',
                        'timestamp': time.time(),
                        'motors_stopped': self.motors_stopped,
                        'motors_disabled': self.motors_disabled,
                        'connection_active': self.is_connection_active
                    }
                    await self._send_telemetry(telemetry)
                else:
                    logger.debug(f"Невідомий тип JSON повідомлення: {data.get('type')}")
            else:
                logger.debug(f"Отримано не-JSON повідомлення: {message}")
                
        except json.JSONDecodeError:
            logger.warning(f"Не вдалося розпарсити JSON: {message}")
        except Exception as e:
            logger.error(f"Помилка обробки повідомлення: {e}")
    
    async def _handle_command(self, data):
        """Обробити команду управління"""
        try:
            if not self.motor_controller:
                logger.warning("Контролер моторів не підключений")
                return
                
            # Оновлюємо час останньої команди
            self.last_command_time = time.time()
            
            # Якщо мотори були зупинені/відключені, включаємо їх знову
            if self.motors_stopped or self.motors_disabled:
                logger.info("♻️  Поновлення роботи моторів після команди")
                await self.motor_controller.enable_all()
                self.motors_stopped = False
                self.motors_disabled = False
            
            command_type = data.get('command')
            
            if command_type == 'move':
                x = data.get('x', 0)
                y = data.get('y', 0)
                
                # Розраховуємо швидкості моторів
                left_speed, right_speed = self._calculate_motor_speeds(x, y)
                
                logger.debug(f"Команда руху: x={x}, y={y} -> left={left_speed}, right={right_speed}")
                
                # Відправляємо команди моторам
                await self.motor_controller.set_motor_speeds(left_speed, right_speed)
                
            elif command_type == 'stop':
                logger.info("Команда зупинки")
                await self.motor_controller.stop_all()
                
        except Exception as e:
            logger.error(f"Помилка обробки команди: {e}")
    
    def _calculate_motor_speeds(self, x, y):
        """
        Розраховує швидкості лівого та правого моторів на основі координат джойстика
        x: -1.0 до 1.0 (ліво-право)
        y: -1.0 до 1.0 (назад-вперед)
        """
        # Прямий рух/назад
        forward = y
        # Поворот
        turn = x
        
        # Розраховуємо швидкості для диференціального приводу
        left_speed = forward + turn
        right_speed = forward - turn
        
        # Обмежуємо значення до [-1.0, 1.0]
        left_speed = max(-1.0, min(1.0, left_speed))
        right_speed = max(-1.0, min(1.0, right_speed))
        
        return left_speed, right_speed
    
    async def _send_message(self, type, data):
        """Відправити повідомлення на сервер"""
        if self.socket and self.socket.open:
            await self.socket.send(f"{type}!{json.dumps(data)}")
    
    async def _send_telemetry(self, data):
        """Відправити телеметрію"""
        await self._send_message("TELEMETRY", data)
    
    async def _cleanup(self):
        """Очистити ресурси"""
        logger.info("Очищення ресурсів")
        await self._stop_motors()

async def main():
    parser = argparse.ArgumentParser(description='Robot Client')
    parser.add_argument('--server', '-s', default='ws://localhost:8080', help='WebSocket server URL')
    parser.add_argument('--debug', '-d', action='store_true', help='Enable debug logging')
    
    args = parser.parse_args()
    
    if args.debug:
        logging.getLogger().setLevel(logging.DEBUG)
    
    # Ініціалізуємо контролер моторів
    motor_controller = None
    try:
        # Імпортуємо та ініціалізуємо контролер моторів
        from motor_controller import MotorController
        motor_controller = MotorController()
        await motor_controller.initialize()
        logger.info("✅ Контролер моторів ініціалізовано")
    except ImportError:
        logger.warning("⚠️  Модуль motor_controller не знайдено - тестовий режим")
    except Exception as e:
        logger.error(f"❌ Помилка ініціалізації контролера моторів: {e}")
    
    # Створюємо клієнт робота
    client = RobotClient(args.server, motor_controller)
    
    try:
        await client.connect()
    except KeyboardInterrupt:
        logger.info("Отримано сигнал переривання")
    except Exception as e:
        logger.error(f"Помилка роботи клієнта: {e}")
    finally:
        if motor_controller:
            await motor_controller.cleanup()

if __name__ == "__main__":
    asyncio.run(main()) 