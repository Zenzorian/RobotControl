# Robot Control Unity Project

Проект для управления роботом через WebSocket соединение с использованием Unity.

## Особенности

- Управление роботом через WebSocket
- Поддержка геймпада и клавиатуры
- Валидация IP-адреса для подключения
- Обработка ошибок соединения
- Украинская локализация интерфейса

## Требования

- Unity 2022.3 или новее
- [Unity WebSocket](https://github.com/mikerochip/unity-websocket.git) пакет

## Установка

1. Клонируйте репозиторий
2. Откройте проект в Unity
3. Установите WebSocket пакет через Package Manager:
   - Window > Package Manager
   - "+ > Add package from git URL..."
   - Введите: `https://github.com/mikerochip/unity-websocket.git`

## Структура проекта

- `Assets/Scripts/Services/RoboClient/` - WebSocket клиент для связи с роботом
- `Assets/Scripts/Services/InputManager/` - Управление вводом (геймпад/клавиатура)
- `Assets/Scripts/UI/` - UI компоненты

## Использование

1. Введите IP-адрес робота
2. Нажмите кнопку подключения
3. Используйте геймпад или клавиатуру для управления:
   - Левый стик/WASD - движение
   - Правый стик/стрелки - поворот камеры
   - Кнопки действий - изменение скорости 