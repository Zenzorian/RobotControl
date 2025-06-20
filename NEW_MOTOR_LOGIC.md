# Новая логика управления моторами

## 🚗 Изменения в поведении робота

### ⏱️ Таймауты:
- **2 секунды** - начало подачи команд остановки
- **60 секунд** - полное отключение управления моторами

### 📋 Этапы работы:

#### 1. Нормальная работа (0-2 секунды без команд)
- Моторы управляются джойстиком
- Команды выполняются нормально

#### 2. Режим остановки (2-60 секунд без команд)
- **Каждые 0.5 секунд** роботу подаются команды остановки `set_motors(0, 0)`
- Флаг `motors_stopped = True`
- В логе: "Постоянная подача команд остановки"

#### 3. Полное отключение (60+ секунд без команд)
- Управление моторами **полностью прекращается**
- Команды джойстика **игнорируются**
- Флаг `motors_disabled = True`
- В логе: "Полное отключение управления моторами"

#### 4. Восстановление (при получении команды)
- Все флаги сбрасываются: `motors_stopped = False`, `motors_disabled = False`
- Управление моторами возобновляется
- В логе: "Получена команда, моторы готовы к работе"

## 🔧 Новые методы и переменные

### Переменные:
```python
self.command_timeout = 2.0           # Начало команд остановки (2 сек)
self.motor_disable_timeout = 60.0    # Полное отключение (60 сек)
self.motors_stopped = False          # Флаг режима остановки
self.motors_disabled = False         # Флаг полного отключения
```

### Методы:
```python
async def _stop_motors(self):        # Подача команд остановки
async def _disable_motors(self):     # Полное отключение управления
```

## 📊 Логика в _safety_check_loop()

```python
if elapsed_time > 60.0:  # motor_disable_timeout
    # Полное отключение
    logger.warning("Полное отключение моторов")
    await self._disable_motors()
    self.motors_disabled = True

elif elapsed_time > 2.0:  # command_timeout  
    # Постоянные команды остановки
    if not self.motors_stopped:
        logger.warning("Постоянная подача команд остановки")
        self.motors_stopped = True
    
    if not self.motors_disabled:
        await self._stop_motors()  # Каждые 0.5 сек

else:
    # Сброс при получении команды
    if self.motors_stopped or self.motors_disabled:
        logger.info("Моторы готовы к работе")
        self.motors_stopped = False
        self.motors_disabled = False
```

## 🚦 Проверки в обработке команд

```python
# Команды джойстика выполняются только если моторы не отключены
if self.motor_controller and not self.motors_disabled:
    left_speed, right_speed = self._calculate_motor_speeds(x, y)
    self.motor_controller.set_motors(left_speed, right_speed)
```

## 🎛️ Параметры командной строки

### Изменение таймаута полного отключения:
```bash
# 30 секунд до полного отключения
python3 robot_client.py --server ws://193.169.240.11:8080 --timeout 30.0 --use-motors

# 2 минуты до полного отключения  
python3 robot_client.py --server ws://193.169.240.11:8080 --timeout 120.0 --use-motors

# По умолчанию: 60 секунд
python3 robot_client.py --server ws://193.169.240.11:8080 --use-motors
```

### Константы (не изменяются через параметры):
- **command_timeout = 2.0 сек** - начало команд остановки
- **safety_check_interval = 0.5 сек** - частота проверки и команд остановки

## 📈 Временная диаграмма

```
Время без команд:     0s  1s  2s  3s  4s  ... 59s  60s  61s  ...
Состояние моторов:    ✅  ✅  🛑  🛑  🛑  ... 🛑   ❌   ❌   ...
Команды остановки:    -   -   ✅  ✅  ✅  ... ✅   ✅   -    ...
Реакция на джойстик:  ✅  ✅  ✅  ✅  ✅  ... ✅   ❌   ❌   ...

✅ = Работает нормально
🛑 = Подача команд остановки  
❌ = Полностью отключено
```

## 🔄 Преимущества новой логики

1. **Мгновенная остановка** - моторы останавливаются через 2 секунды
2. **Надежная остановка** - команды остановки подаются постоянно 58 секунд
3. **Энергосбережение** - после 60 секунд управление полностью отключается
4. **Безопасность** - отключенные моторы не реагируют на случайные команды
5. **Быстрое восстановление** - при получении команды все возобновляется мгновенно

## 🚨 Важные моменты

- **WebSocket соединение** остается активным всегда
- **Видеопоток** продолжает работать
- **Телеметрия** отправляется даже с отключенными моторами
- **Только управление моторами** отключается для безопасности 