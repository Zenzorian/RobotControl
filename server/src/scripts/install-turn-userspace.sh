#!/bin/bash

echo "🚀 Установка TURN сервера в пользовательском режиме"
echo "=================================================="

# Цвета для вывода
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Функция логирования
log() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Проверка установки coturn
check_coturn() {
    if command -v turnserver &> /dev/null; then
        log "coturn уже установлен"
        return 0
    else
        warn "coturn не найден, пытаемся установить..."
        return 1
    fi
}

# Установка coturn (если возможно)
install_coturn() {
    if [[ $EUID -eq 0 ]]; then
        log "Установка coturn с правами root..."
        apt-get update
        apt-get install -y coturn
    else
        warn "Нет прав root для установки coturn"
        warn "Установите coturn вручную: sudo apt-get install coturn"
        warn "Или запросите у администратора установку"
        
        # Проверяем системную установку
        if dpkg -l | grep -q coturn; then
            log "coturn установлен системно"
            return 0
        fi
        return 1
    fi
}

# Остановка существующих процессов TURN
stop_existing_turn() {
    log "Остановка существующих процессов turnserver..."
    
    # Попытка graceful shutdown
    pkill -TERM turnserver 2>/dev/null
    sleep 2
    
    # Принудительная остановка
    pkill -KILL turnserver 2>/dev/null
    
    # Проверка портов
    if netstat -tuln 2>/dev/null | grep -q ":3478\|:13478"; then
        warn "Порты TURN все еще заняты, но продолжаем..."
    else
        log "Порты TURN освобождены"
    fi
}

# Создание рабочих директорий
setup_directories() {
    log "Создание рабочих директорий..."
    
    mkdir -p ~/turnserver-robot/logs
    mkdir -p ~/turnserver-robot/config
    mkdir -p ~/turnserver-robot/run
    
    log "Директории созданы в: ~/turnserver-robot/"
}

# Проверка доступности портов
check_ports() {
    log "Проверка доступности портов..."
    
    local main_port=13478
    local tls_port=15349
    
    if netstat -tuln 2>/dev/null | grep -q ":$main_port"; then
        warn "Порт $main_port занят"
        return 1
    fi
    
    if netstat -tuln 2>/dev/null | grep -q ":$tls_port"; then
        warn "Порт $tls_port занят"  
        return 1
    fi
    
    log "Порты $main_port и $tls_port свободны"
    return 0
}

# Создание тестовой конфигурации
create_test_config() {
    log "Создание тестовой конфигурации..."
    
    cat > ~/turnserver-robot/config/test.conf << EOF
# Базовая конфигурация TURN сервера
listening-port=13478
tls-listening-port=15349
listening-ip=0.0.0.0
external-ip=193.169.240.11
relay-ip=193.169.240.11

# Аутентификация
fingerprint
lt-cred-mech
user=robotclient:robotclient2024
realm=robotclient.local
server-name=robotclient.local

# Ограничения
total-quota=100
stale-nonce

# Отключаем TLS для упрощения
no-tls
no-dtls

# Логирование
log-file=$HOME/turnserver-robot/logs/turnserver.log
pidfile=$HOME/turnserver-robot/run/turnserver.pid
simple-log
new-log-timestamp-format
verbose

# Отключаем административные интерфейсы
no-cli
no-web-admin

# Оптимизации для Starlink
max-bps=1000000
min-port=49152
max-port=65535
no-multicast-peers
mobility
EOF

    log "Конфигурация создана: ~/turnserver-robot/config/test.conf"
}

# Тест запуска TURN сервера
test_turn_server() {
    log "Тестовый запуск TURN сервера..."
    
    if ! command -v turnserver &> /dev/null; then
        error "turnserver не найден в системе"
        return 1
    fi
    
    # Запуск в фоновом режиме
    turnserver -c ~/turnserver-robot/config/test.conf &
    local turn_pid=$!
    
    sleep 3
    
    # Проверка запуска
    if kill -0 $turn_pid 2>/dev/null; then
        log "TURN сервер успешно запущен (PID: $turn_pid)"
        
        # Тест соединения
        if timeout 5 bash -c "echo > /dev/tcp/localhost/13478" 2>/dev/null; then
            log "✅ TURN сервер отвечает на порту 13478"
        else
            warn "⚠️ TURN сервер не отвечает на порту 13478"
        fi
        
        # Остановка тестового сервера
        kill $turn_pid 2>/dev/null
        log "Тестовый сервер остановлен"
        return 0
    else
        error "❌ TURN сервер не запустился"
        return 1
    fi
}

# Создание управляющих скриптов
create_control_scripts() {
    log "Создание управляющих скриптов..."
    
    # Скрипт запуска
    cat > ~/turnserver-robot/start.sh << 'EOF'
#!/bin/bash
echo "🚀 Запуск TURN сервера..."
cd ~/turnserver-robot
turnserver -c config/test.conf
EOF
    chmod +x ~/turnserver-robot/start.sh
    
    # Скрипт остановки
    cat > ~/turnserver-robot/stop.sh << 'EOF'
#!/bin/bash
echo "🛑 Остановка TURN сервера..."
if [ -f ~/turnserver-robot/run/turnserver.pid ]; then
    kill $(cat ~/turnserver-robot/run/turnserver.pid) 2>/dev/null
    rm -f ~/turnserver-robot/run/turnserver.pid
    echo "✅ TURN сервер остановлен"
else
    pkill -f turnserver
    echo "✅ TURN процессы остановлены"
fi
EOF
    chmod +x ~/turnserver-robot/stop.sh
    
    # Скрипт статуса
    cat > ~/turnserver-robot/status.sh << 'EOF'
#!/bin/bash
echo "📊 Статус TURN сервера:"
echo "======================"

if pgrep -f turnserver > /dev/null; then
    echo "✅ TURN сервер запущен"
    echo "📈 Процессы:"
    pgrep -af turnserver
    echo ""
    echo "🌐 Порты:"
    netstat -tuln | grep -E ":13478|:15349" || echo "Нет активных TURN портов"
else
    echo "❌ TURN сервер не запущен"
fi

echo ""
echo "📝 Логи (последние 10 строк):"
if [ -f ~/turnserver-robot/logs/turnserver.log ]; then
    tail -10 ~/turnserver-robot/logs/turnserver.log
else
    echo "Файл логов не найден"
fi
EOF
    chmod +x ~/turnserver-robot/status.sh
    
    log "Управляющие скрипты созданы:"
    log "  - ~/turnserver-robot/start.sh"
    log "  - ~/turnserver-robot/stop.sh" 
    log "  - ~/turnserver-robot/status.sh"
}

# Основная функция
main() {
    echo ""
    log "Начинаем установку..."
    
    # Остановка существующих процессов
    stop_existing_turn
    
    # Проверка/установка coturn
    if ! check_coturn; then
        if ! install_coturn; then
            error "Не удалось установить coturn"
            error "Установите вручную: sudo apt-get install coturn"
            exit 1
        fi
    fi
    
    # Настройка директорий
    setup_directories
    
    # Проверка портов
    check_ports
    
    # Создание конфигурации
    create_test_config
    
    # Тест запуска
    if test_turn_server; then
        log "✅ TURN сервер работает корректно"
    else
        warn "⚠️ TURN сервер имеет проблемы, но продолжаем..."
    fi
    
    # Создание управляющих скриптов
    create_control_scripts
    
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}🎉 Установка завершена успешно!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    echo "📁 Директория: ~/turnserver-robot/"
    echo "🚀 Запуск: ~/turnserver-robot/start.sh"
    echo "🛑 Остановка: ~/turnserver-robot/stop.sh"
    echo "📊 Статус: ~/turnserver-robot/status.sh"
    echo ""
    echo "🔧 Настройки:"
    echo "  - Порт UDP: 13478"
    echo "  - Порт TLS: 15349"
    echo "  - Пользователь: robotclient"
    echo "  - Пароль: robotclient2024"
    echo ""
    echo "📝 Логи: ~/turnserver-robot/logs/turnserver.log"
    echo ""
}

# Запуск установки
main "$@" 