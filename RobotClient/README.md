# Robot Client для Linux

Этот проект содержит классы для работы с USB устройствами в Linux:
- `CameraSearch` - поиск и идентификация USB веб-камер
- `PixhawkControl` - управление полетным контроллером Pixhawk через USB

## Возможности

### 📷 CameraSearch
- **Автоматическое обнаружение USB веб-камер** в Linux
- **Множественные методы поиска**: через `v4l2-ctl` и `udevadm`
- **Детальная информация об устройствах**: производитель, модель, разрешения, форматы
- **Интеллектуальная фильтрация**: различает камеры и другие видеоустройства

### 🚁 PixhawkControl
- **Автоматическое обнаружение Pixhawk** устройств через USB
- **Официальная MAVLink библиотека** для надежной связи
- **PWM управление моторами** с диапазоном скоростей -1.0 до 1.0
- **Безопасные операции**: автоматическое переподключение и остановка моторов
- **Поддержка ArduPilot и PX4** прошивок

## Системные требования

### Обязательные
- Linux (Debian/Ubuntu и другие дистрибутивы)
- .NET 8.0

### Рекомендуемые пакеты
```bash
sudo apt update
sudo apt install v4l-utils udev
```

## Права доступа

### Для веб-камер
```bash
# Добавить пользователя в группу video
sudo usermod -a -G video $USER
```

### Для Pixhawk (последовательные порты)
```bash
# Добавить пользователя в группу dialout
sudo usermod -a -G dialout $USER

# Перелогиниться или выполнить
newgrp dialout
```

## Использование

### Поиск веб-камер
```csharp
using RobotClient.Camera;

var cameraSearch = new CameraSearch();
var cameras = cameraSearch.FindUsbWebcams();

foreach (var camera in cameras)
{
    Console.WriteLine($"Найдена камера: {camera.Name}");
    Console.WriteLine($"  Устройство: {camera.DevicePath}");
    Console.WriteLine($"  Производитель: {camera.Manufacturer}");
    Console.WriteLine($"  Разрешения: {string.Join(", ", camera.SupportedResolutions)}");
}
```

### Управление Pixhawk
```csharp
using RobotClient.Pixhawk;

using var pixhawk = new PixhawkControl();

// Подключение
if (pixhawk.Connect())
{
    Console.WriteLine("Pixhawk подключен!");
    
    // Движение вперед
    pixhawk.SetWheelSpeeds(0.5f, 0.5f);
    Thread.Sleep(2000);
    
    // Поворот направо
    pixhawk.SetWheelSpeeds(0.5f, -0.5f);
    Thread.Sleep(1000);
    
    // Остановка
    pixhawk.StopMotors();
}
```

## Запуск

```bash
dotnet build
dotnet run
```

## Функции программы

При запуске программа выполняет:

1. **Поиск USB веб-камер** - сканирует и отображает найденные камеры
2. **Подключение к Pixhawk** - ищет и подключается к полетному контроллеру
3. **Тестирование управления** - выполняет тестовые движения (если Pixhawk подключен)
4. **Интерактивное управление** - позволяет управлять роботом с клавиатуры:
   - `W` - движение вперед
   - `S` - движение назад  
   - `A` - поворот влево
   - `D` - поворот вправо
   - `X` - остановка
   - `Q` - выход

## Отладка

### Веб-камеры
Если камеры не найдены:
```bash
# Проверить наличие устройств
ls /dev/video*

# Информация об устройствах
v4l2-ctl --list-devices

# USB устройства
lsusb | grep -i camera
```

### Pixhawk
Если Pixhawk не найден:
```bash
# Проверить последовательные порты
ls /dev/ttyACM* /dev/ttyUSB*

# Информация об USB устройствах
lsusb | grep -E "(3DR|Pixhawk|PX4)"

# Права доступа к портам
ls -la /dev/ttyACM*

# Проверка группы dialout
groups $USER
```

## Архитектура классов

### CameraSearch
```csharp
public class CameraSearch
{
    public List<CameraInfo> FindUsbWebcams() // Основной метод поиска
}

public class CameraInfo
{
    public string Name { get; set; }
    public string DevicePath { get; set; }    // /dev/video0
    public string Manufacturer { get; set; }
    public List<string> SupportedResolutions { get; set; }
}
```

### PixhawkControl
```csharp
public class PixhawkControl : IDisposable
{
    public bool IsConnected()                           // Проверка подключения
    public bool Connect()                               // Поиск и подключение
    public bool SetWheelSpeeds(float left, float right) // Управление моторами
    public bool StopMotors()                            // Остановка
    public void Disconnect()                            // Отключение
}
```

## Примечания

- Проект оптимизирован только для Linux
- CameraSearch поддерживает стандартные USB UVC камеры
- PixhawkControl использует MAVLink протокол
- Все методы включают обработку ошибок и логирование
- Безопасное завершение работы с автоматической остановкой моторов 

## MAVLink Integration

Проект использует официальную MAVLink библиотеку версии 1.0.8:

### Основные возможности:
- **Heartbeat сообщения** для поддержания связи
- **RC_CHANNELS_OVERRIDE** для управления моторами
- **Автоматический парсинг** входящих сообщений
- **Поддержка MAVLink 2.0** протокола

### Поддерживаемые устройства:
- **ArduPilot** (Copter, Rover, Plane)
- **PX4** автопилот
- **Pixhawk**, **Cube**, **Holybro** и другие контроллеры

## Отладка

### Проверка USB устройств:
```bash
# Список всех USB устройств
lsusb

# Информация о последовательных портах
ls -la /dev/ttyACM* /dev/ttyUSB*

# Подробная информация об устройстве
udevadm info --name=/dev/ttyACM0
```

### Проверка камер:
```bash
# Список видеоустройств
v4l2-ctl --list-devices

# Возможности камеры
v4l2-ctl --device=/dev/video0 --list-formats-ext
```

### Тестирование MAVLink:
```bash
# Мониторинг трафика на порту
sudo minicom -D /dev/ttyACM0 -b 57600

# Или с помощью screen
screen /dev/ttyACM0 57600
```

## Примеры конфигурации

### ArduPilot Rover
```
SYSID_THISMAV = 1
SERIAL2_PROTOCOL = 2  # MAVLink 2
SERIAL2_BAUD = 57     # 57600 baud
```

### PX4
```
MAV_SYS_ID = 1
SER_TEL2_BAUD = 57600
```

## Устранение неполадок

### Pixhawk не обнаруживается:
1. Проверьте USB подключение
2. Убедитесь в правах доступа (группа `dialout`)
3. Проверьте, что устройство не используется другим приложением
4. Попробуйте разные USB порты

### Камера не найдена:
1. Проверьте USB подключение камеры
2. Убедитесь в правах доступа (группа `video`)
3. Проверьте поддержку V4L2: `ls /dev/video*`

### Ошибки MAVLink:
1. Проверьте скорость подключения (обычно 57600 или 115200)
2. Убедитесь, что MAVLink включен в прошивке
3. Проверьте правильность system ID и component ID

## Лицензия

MIT License - см. файл LICENSE для подробностей. 