#!/bin/bash

echo "🔍 TURN Server Diagnostics"
echo "=========================="

# Цвета для вывода
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Функции логирования
log() { echo -e "${GREEN}[✓]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
error() { echo -e "${RED}[✗]${NC} $1"; }
info() { echo -e "${BLUE}[i]${NC} $1"; }

echo ""
info "Начинаем диагностику..."
echo ""

# 1. Проверка установки coturn
echo "1️⃣ Проверка установки coturn"
echo "----------------------------"
if command -v turnserver &> /dev/null; then
    log "coturn установлен: $(which turnserver)"
    turnserver --version 2>/dev/null | head -1 || echo "Версия не определена"
else
    error "coturn НЕ установлен"
    warn "Установите: sudo apt-get install coturn"
fi
echo ""

# 2. Проверка портов
echo "2️⃣ Проверка портов"
echo "-------------------"

check_port() {
    local port=$1
    local protocol=$2
    if netstat -tuln 2>/dev/null | grep -q ":$port"; then
        error "Порт $port/$protocol ЗАНЯТ"
        netstat -tulnp 2>/dev/null | grep ":$port" | head -5
        return 1
    else
        log "Порт $port/$protocol свободен"
        return 0
    fi
}

# Старые порты (должны быть свободны)
check_port 3478 "UDP (старый TURN)"
check_port 5349 "TCP (старый TURNS)"

# Новые порты (должны быть свободны для запуска)
check_port 13478 "UDP (новый TURN)"
check_port 15349 "TCP (новый TURNS)"

echo ""

# 3. Проверка процессов TURN
echo "3️⃣ Проверка процессов TURN"
echo "---------------------------"
turn_processes=$(pgrep -af turnserver)
if [ -n "$turn_processes" ]; then
    warn "Найдены активные процессы turnserver:"
    echo "$turn_processes"
    echo ""
    warn "Рекомендация: остановите существующие процессы"
    echo "  sudo pkill -f turnserver"
else
    log "Нет активных процессов turnserver"
fi
echo ""

# 4. Проверка конфигурационных файлов
echo "4️⃣ Проверка конфигурации"
echo "-------------------------"

config_files=(
    "/tmp/turnserver-robot.conf"
    "~/turnserver-robot/config/test.conf"
    "/etc/turnserver.conf"
)

for config in "${config_files[@]}"; do
    expanded_path=$(eval echo "$config")
    if [ -f "$expanded_path" ]; then
        log "Конфигурация найдена: $expanded_path"
        info "Размер: $(ls -lh "$expanded_path" | awk '{print $5}')"
        info "Последнее изменение: $(stat -c %y "$expanded_path" 2>/dev/null | cut -d. -f1)"
    else
        warn "Конфигурация НЕ найдена: $expanded_path"
    fi
done
echo ""

# 5. Проверка логов
echo "5️⃣ Проверка логов"
echo "------------------"

log_files=(
    "/tmp/turnserver-robot.log"
    "~/turnserver-robot/logs/turnserver.log"
    "/var/log/turnserver.log"
)

for log_file in "${log_files[@]}"; do
    expanded_path=$(eval echo "$log_file")
    if [ -f "$expanded_path" ]; then
        log "Лог найден: $expanded_path"
        info "Размер: $(ls -lh "$expanded_path" | awk '{print $5}')"
        info "Последние ошибки:"
        tail -5 "$expanded_path" | grep -i error || echo "  Нет недавних ошибок"
    else
        warn "Лог НЕ найден: $expanded_path"
    fi
done
echo ""

# 6. Проверка сетевого подключения
echo "6️⃣ Проверка сетевого подключения"
echo "---------------------------------"

# Проверка доступности сервера
info "Проверка доступности 193.169.240.11..."
if ping -c 1 -W 3 193.169.240.11 &>/dev/null; then
    log "Сервер 193.169.240.11 доступен"
else
    error "Сервер 193.169.240.11 НЕДОСТУПЕН"
fi

# Проверка DNS
info "Проверка DNS..."
if nslookup google.com &>/dev/null; then
    log "DNS работает"
else
    warn "Проблемы с DNS"
fi
echo ""

# 7. Тест сокетов
echo "7️⃣ Тест TCP сокетов"
echo "--------------------"

test_socket() {
    local host=$1
    local port=$2
    local timeout=3
    
    if timeout $timeout bash -c "echo > /dev/tcp/$host/$port" 2>/dev/null; then
        log "Соединение с $host:$port успешно"
        return 0
    else
        error "Не удается подключиться к $host:$port"
        return 1
    fi
}

# Тест подключения к новым портам
test_socket "localhost" "13478"
test_socket "193.169.240.11" "13478"
echo ""

# 8. Проверка прав доступа
echo "8️⃣ Проверка прав доступа"
echo "-------------------------"

# Проверка записи в /tmp
if touch /tmp/turnserver-test 2>/dev/null; then
    log "Запись в /tmp доступна"
    rm -f /tmp/turnserver-test
else
    error "Нет прав записи в /tmp"
fi

# Проверка домашней директории
if touch ~/turnserver-test 2>/dev/null; then
    log "Запись в домашнюю директорию доступна"
    rm -f ~/turnserver-test
else
    error "Нет прав записи в домашнюю директорию"
fi
echo ""

# 9. Проверка Node.js сервера
echo "9️⃣ Проверка Node.js сервера"
echo "----------------------------"

if pgrep -f "node.*index.js" > /dev/null; then
    log "Node.js сервер запущен"
    info "PID: $(pgrep -f "node.*index.js")"
else
    warn "Node.js сервер НЕ запущен"
fi

# Проверка порта 8080
if netstat -tuln 2>/dev/null | grep -q ":8080"; then
    log "Порт 8080 (Node.js) слушает"
else
    warn "Порт 8080 НЕ слушает"
fi
echo ""

# 10. Системная информация
echo "🔟 Системная информация"
echo "------------------------"
info "Операционная система: $(lsb_release -d 2>/dev/null | cut -f2 || uname -o)"
info "Версия ядра: $(uname -r)"
info "Архитектура: $(uname -m)"
info "Пользователь: $(whoami)"
info "Текущая директория: $(pwd)"
echo ""

# 11. Рекомендации
echo "💡 Рекомендации по устранению проблем"
echo "======================================"

recommendations=()

# Анализ найденных проблем
if ! command -v turnserver &> /dev/null; then
    recommendations+=("🔧 Установите coturn: sudo apt-get update && sudo apt-get install coturn")
fi

if pgrep -f turnserver > /dev/null; then
    recommendations+=("🛑 Остановите существующие TURN процессы: sudo pkill -f turnserver")
fi

if netstat -tuln 2>/dev/null | grep -q ":3478\|:5349"; then
    recommendations+=("⚠️ Освободите стандартные TURN порты или используйте альтернативные")
fi

if ! ping -c 1 -W 3 193.169.240.11 &>/dev/null; then
    recommendations+=("🌐 Проверьте сетевое подключение к серверу 193.169.240.11")
fi

if [ ${#recommendations[@]} -eq 0 ]; then
    log "🎉 Серьезных проблем не обнаружено!"
    info "Попробуйте запустить сервер: npm start"
else
    for rec in "${recommendations[@]}"; do
        echo "  $rec"
    done
fi

echo ""
echo "📚 Дополнительная помощь:"
echo "  - Полное руководство: /RobotClient/TURN_TROUBLESHOOTING.md"
echo "  - Установка в пользовательском режиме: ./src/scripts/install-turn-userspace.sh"
echo "  - Логи Node.js: npm start 2>&1 | tee server.log"
echo ""

info "Диагностика завершена" 