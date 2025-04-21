# Клієнт робота для WebRTC керування

Цей проєкт забезпечує підключення робота на базі Raspberry Pi до сервера WebSocket для прийому команд керування та передачі відеопотоку через WebRTC.

## Вимоги

- Raspberry Pi 4 (або новіше)
- USB веб-камера
- Привід моторів (наприклад, L298N)
- Python 3.7 або новіше

## Встановлення

1. Встановіть залежності Python:

```bash
pip install -r requirements.txt
```

2. Встановіть системні залежності для aiortc:

```bash
sudo apt-get update
sudo apt-get install -y libavdevice-dev libavfilter-dev libavformat-dev libavcodec-dev libswresample-dev libswscale-dev libavutil-dev libsrtp2-dev libopus-dev libvpx-dev
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

## Запуск

### Симуляційний режим (без моторів)

```bash
python robot_client.py --server ws://193.169.240.11:8080
```

### Режим з керуванням моторами

```bash
python robot_client.py --server ws://193.169.240.11:8080 --use-motors
```

### Додаткові параметри

- `--server` - URL WebSocket-сервера (за замовчуванням: ws://193.169.240.11:8080)
- `--camera` - Індекс USB-камери (за замовчуванням: 0)
- `--use-motors` - Увімкнути керування реальними моторами через GPIO

## Тестування моторів

Ви можете самостійно протестувати керування моторами без підключення до сервера:

```bash
python motor_controller.py
```

## Протокол обміну

Робот спілкується з сервером через наступні повідомлення:

- `REGISTER!ROBOT` - Реєстрація на сервері як робот
- `COMMAND!<json_data>` - Отримання команд управління
- `OFFER!<sdp_data>` - Отримання WebRTC пропозиції для відео
- `ANSWER!<sdp_data>` - Відправлення WebRTC відповіді
- `CANDIDATE!<ice_data>` - Обмін ICE кандидатами
- `TELEMETRY!<json_data>` - Відправлення телеметрії 