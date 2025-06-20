# Клієнт робота для управління моторами

Цей проєкт забезпечує підключення робота на базі Raspberry Pi до сервера WebSocket для прийому команд керування моторами.

## Вимоги

- Raspberry Pi 4 (або новіше)
- Привід моторів (наприклад, L298N або Pixhawk)
- Python 3.7 або новіше

## Встановлення

### Стандартний контролер (GPIO)

```bash
pip install -r requirements.txt
```

### Pixhawk контролер

```bash
pip install -r requirements-pixhawk.txt
```

## Підключення апаратного забезпечення

### Підключення моторів (L298N)

За замовчуванням використовуються наступні GPIO піни:

- Лівий мотор:
  - ENABLE: GPIO 12
  - IN1: GPIO 16
  - IN2: GPIO 18

- Правий мотор:
  - ENABLE: GPIO 13
  - IN1: GPIO 22
  - IN2: GPIO 24

Ви можете змінити ці піни у файлі `motor_controller.py`.

### Підключення Pixhawk

Для використання Pixhawk як контролера моторів підключіть його через USB або UART.

## Запуск

### Симуляційний режим (без моторів)

```bash
python robot_client.py --server ws://localhost:8080
```

### Тестування з керуванням моторами

```bash
python robot_client.py --server ws://localhost:8080 --debug
```

### Додаткові параметри

- `--server` - URL WebSocket-сервера (за замовчуванням: ws://localhost:8080)
- `--debug` - Увімкнути детальне логування

## Тестування моторів

Ви можете самостійно протестувати керування моторами без підключення до сервера:

```bash
python motor_controller.py
```

## Протокол обміну

Робот спілкується з сервером через наступні повідомлення:

- `REGISTER!ROBOT` - Реєстрація на сервері як робот
- JSON команди з типом `command` для управління моторами
- JSON телеметрія для відправлення статусу

## Безпека

- Автоматична зупинка моторів при втраті зв'язку (2 секунди)
- Повне відключення після 60 секунд бездіяльності
- Система моніторингу з'єднання

## Відео

Для передачі відео використовуйте `optimized_video_client.py` - він працює незалежно від системи управління моторами та використовує оптимізований протокол без WebRTC. 