import RPi.GPIO as GPIO
import time

class MotorController:
    # Налаштування GPIO пінів для моторів
    # Приклад для L298N драйвера моторів
    LEFT_MOTOR_ENABLE = 12
    LEFT_MOTOR_IN1 = 16
    LEFT_MOTOR_IN2 = 18
    
    RIGHT_MOTOR_ENABLE = 13
    RIGHT_MOTOR_IN1 = 22
    RIGHT_MOTOR_IN2 = 24
    
    # ШІМ частота (Гц)
    PWM_FREQ = 1000
    
    def __init__(self):
        # Налаштування GPIO
        GPIO.setmode(GPIO.BCM)
        GPIO.setwarnings(False)
        
        # Налаштування пінів лівого мотора
        GPIO.setup(self.LEFT_MOTOR_ENABLE, GPIO.OUT)
        GPIO.setup(self.LEFT_MOTOR_IN1, GPIO.OUT)
        GPIO.setup(self.LEFT_MOTOR_IN2, GPIO.OUT)
        
        # Налаштування пінів правого мотора
        GPIO.setup(self.RIGHT_MOTOR_ENABLE, GPIO.OUT)
        GPIO.setup(self.RIGHT_MOTOR_IN1, GPIO.OUT)
        GPIO.setup(self.RIGHT_MOTOR_IN2, GPIO.OUT)
        
        # Налаштування ШІМ для керування швидкістю
        self.left_pwm = GPIO.PWM(self.LEFT_MOTOR_ENABLE, self.PWM_FREQ)
        self.right_pwm = GPIO.PWM(self.RIGHT_MOTOR_ENABLE, self.PWM_FREQ)
        
        # Запуск ШІМ з нульовим робочим циклом (зупинка)
        self.left_pwm.start(0)
        self.right_pwm.start(0)
    
    def set_motor_speed(self, motor_side, speed):
        """
        Встановлює швидкість та напрямок мотора
        
        :param motor_side: "left" або "right"
        :param speed: значення від -1.0 до 1.0 (від'ємні - назад, додатні - вперед)
        """
        # Перетворюємо швидкість у відсотки для ШІМ (0-100)
        pwm_value = abs(speed) * 100
        
        if motor_side == "left":
            if speed >= 0:  # Рух вперед
                GPIO.output(self.LEFT_MOTOR_IN1, GPIO.HIGH)
                GPIO.output(self.LEFT_MOTOR_IN2, GPIO.LOW)
            else:  # Рух назад
                GPIO.output(self.LEFT_MOTOR_IN1, GPIO.LOW)
                GPIO.output(self.LEFT_MOTOR_IN2, GPIO.HIGH)
            
            self.left_pwm.ChangeDutyCycle(pwm_value)
            
        elif motor_side == "right":
            if speed >= 0:  # Рух вперед
                GPIO.output(self.RIGHT_MOTOR_IN1, GPIO.HIGH)
                GPIO.output(self.RIGHT_MOTOR_IN2, GPIO.LOW)
            else:  # Рух назад
                GPIO.output(self.RIGHT_MOTOR_IN1, GPIO.LOW)
                GPIO.output(self.RIGHT_MOTOR_IN2, GPIO.HIGH)
            
            self.right_pwm.ChangeDutyCycle(pwm_value)
    
    def stop(self):
        """Зупиняє обидва мотори"""
        self.left_pwm.ChangeDutyCycle(0)
        self.right_pwm.ChangeDutyCycle(0)
    
    def cleanup(self):
        """Звільняє ресурси GPIO"""
        self.stop()
        self.left_pwm.stop()
        self.right_pwm.stop()
        GPIO.cleanup()

# Приклад використання
if __name__ == "__main__":
    try:
        controller = MotorController()
        print("Тестуємо мотори...")
        
        # Рух вперед протягом 2 секунд
        print("Рух вперед")
        controller.set_motor_speed("left", 0.7)
        controller.set_motor_speed("right", 0.7)
        time.sleep(2)
        
        # Зупинка
        print("Зупинка")
        controller.stop()
        time.sleep(1)
        
        # Поворот вліво
        print("Поворот вліво")
        controller.set_motor_speed("left", -0.5)
        controller.set_motor_speed("right", 0.5)
        time.sleep(1)
        
        # Зупинка
        print("Зупинка")
        controller.stop()
        time.sleep(1)
        
        # Поворот вправо
        print("Поворот вправо")
        controller.set_motor_speed("left", 0.5)
        controller.set_motor_speed("right", -0.5)
        time.sleep(1)
        
        # Зупинка
        print("Зупинка")
        controller.stop()
        
    except KeyboardInterrupt:
        print("Завершення роботи...")
    finally:
        if 'controller' in locals():
            controller.cleanup() 