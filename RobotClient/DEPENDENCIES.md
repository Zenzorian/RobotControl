# Зависимости проекта RobotClient

## ✅ Статус проекта
- **Сборка**: УСПЕШНО ✓
- **Зависимости**: ВСЕ УСТАНОВЛЕНЫ ✓
- **Предупреждения**: ИСПРАВЛЕНЫ ✓

## 📦 Основные зависимости (NuGet)

### MAVLink 1.0.8
- **Назначение**: Официальная библиотека для работы с MAVLink протоколом
- **Статус**: ✅ Установлен и работает
- **Использование**: Связь с Pixhawk/ArduPilot/PX4
- **Транзитивные зависимости**:
  - Newtonsoft.Json 12.0.3
  - System.Runtime.CompilerServices.Unsafe 4.7.0

### System.IO.Ports 8.0.0  
- **Назначение**: Работа с последовательными портами (USB/Serial)
- **Статус**: ✅ Установлен и работает
- **Использование**: Подключение к Pixhawk через USB
- **Транзитивные зависимости**:
  - runtime.native.System.IO.Ports 8.0.0
  - runtime.linux-x64.runtime.native.System.IO.Ports 8.0.0
  - runtime.linux-arm64.runtime.native.System.IO.Ports 8.0.0
  - runtime.osx-x64.runtime.native.System.IO.Ports 8.0.0
  - runtime.osx-arm64.runtime.native.System.IO.Ports 8.0.0

## 🎯 Целевая платформа
- **.NET 8.0** - современная LTS версия
- **Cross-platform** поддержка (Windows, Linux, macOS)

## 🛠️ Команды для работы с зависимостями

```bash
# Восстановление пакетов
dotnet restore

# Просмотр установленных пакетов
dotnet list package

# Просмотр всех зависимостей (включая транзитивные)
dotnet list package --include-transitive

# Обновление пакетов (осторожно!)
dotnet add package PackageName --version X.Y.Z

# Сборка проекта
dotnet build

# Запуск проекта
dotnet run
```

## 🔍 Проверка зависимостей

### Проверка MAVLink
```csharp
// Тест создания MAVLink parser
var parser = new MAVLink.MavlinkParse();
Console.WriteLine("MAVLink инициализирован успешно");
```

### Проверка System.IO.Ports
```csharp
// Тест доступности портов
var ports = System.IO.Ports.SerialPort.GetPortNames();
Console.WriteLine($"Найдено портов: {ports.Length}");
```

## 📋 Детали установки

### Команды установки (если нужно переустановить):
```bash
# Установка MAVLink
dotnet add package MAVLink --version 1.0.8

# Установка System.IO.Ports  
dotnet add package System.IO.Ports --version 8.0.0
```

### Минимальные системные требования:
- **.NET 8.0 Runtime** или выше
- **Linux**: `libc6`, `libgcc1`, `libssl` (обычно уже установлены)
- **Права доступа**: группы `dialout` и `video`

## ⚠️ Важные замечания

1. **MAVLink 1.0.8** - стабильная версия, поддерживает MAVLink 2.0 протокол
2. **System.IO.Ports** требует нативные библиотеки для каждой платформы
3. **Cross-platform совместимость** обеспечена автоматически
4. **Безопасность**: все пакеты из официальных источников Microsoft/ArduPilot

## 🔄 Последнее обновление
- **Дата**: 29 июня 2025
- **Статус**: Все зависимости актуальны и работают корректно
- **Проверено**: Сборка, восстановление пакетов, отсутствие конфликтов

---
*Автоматически проверено: dotnet build && dotnet list package --include-transitive* 