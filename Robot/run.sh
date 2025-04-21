#!/bin/bash

# Перевірка наявності аргументів
if [ "$1" == "--help" ] || [ "$1" == "-h" ]; then
  echo "Скрипт запуску клієнта робота"
  echo "Використання: ./run.sh [опції]"
  echo
  echo "Опції:"
  echo "  --with-motors    Увімкнути керування фізичними моторами"
  echo "  --server URL     Встановити URL сервера (за замовчуванням: ws://193.169.240.11:8080)"
  echo "  --camera IDX     Встановити індекс камери (за замовчуванням: 0)"
  echo "  --debug          Увімкнути детальне логування"
  exit 0
fi

# Встановлення змінних за замовчуванням
SERVER_URL="ws://193.169.240.11:8080"
CAMERA_IDX=0
USE_MOTORS=false
DEBUG_MODE=false

# Обробка аргументів
while [ "$1" != "" ]; do
  case $1 in
    --with-motors)
      USE_MOTORS=true
      ;;
    --server)
      shift
      SERVER_URL="$1"
      ;;
    --camera)
      shift
      CAMERA_IDX="$1"
      ;;
    --debug)
      DEBUG_MODE=true
      ;;
  esac
  shift
done

# Підготовка команди запуску
CMD="python robot_client.py --server $SERVER_URL --camera $CAMERA_IDX"

if [ "$USE_MOTORS" = true ]; then
  CMD="$CMD --use-motors"
fi

if [ "$DEBUG_MODE" = true ]; then
  CMD="$CMD --debug"
fi

# Виведення повідомлення про запуск
echo "Запуск робота з налаштуваннями:"
echo "Сервер: $SERVER_URL"
echo "Камера: $CAMERA_IDX"
if [ "$USE_MOTORS" = true ]; then
  echo "Режим: з керуванням моторами"
else
  echo "Режим: симуляція (без керування моторами)"
fi
if [ "$DEBUG_MODE" = true ]; then
  echo "Логування: детальний вивід"
fi
echo "---"

# Виконання команди
$CMD 