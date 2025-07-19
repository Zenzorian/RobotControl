#!/bin/bash

# Скрипт установки и настройки TURN-сервера для робота
# Запускать с правами root на сервере 193.169.240.11

set -e

echo "🔄 Установка TURN-сервера (coturn) для робота..."

# Обновление системы
echo "📦 Обновление системы..."
apt-get update -y
apt-get upgrade -y

# Установка coturn
echo "📦 Установка coturn..."
apt-get install -y coturn

# Создание конфигурационного файла
echo "📝 Создание конфигурации TURN-сервера..."
cat > /etc/turnserver.conf << 'EOF'
# TURN server configuration for Robot WebRTC

# Listening ports
listening-port=3478
tls-listening-port=5349

# IP addresses
listening-ip=0.0.0.0
external-ip=193.169.240.11
relay-ip=193.169.240.11

# Authentication
lt-cred-mech
user=robotclient:robotclient2024
realm=robotclient.local

# Security
fingerprint
server-name=robotclient.local

# Quotas and limits
total-quota=100
stale-nonce

# Logging
log-file=/var/log/turnserver.log
verbose

# SSL certificates (самоподписанные для тестирования)
cert=/etc/ssl/certs/turn-server.crt
pkey=/etc/ssl/private/turn-server.key

# Optimizations for Starlink/Satellite connections
max-bps=1000000
min-port=49152
max-port=65535
no-multicast-peers
mobility

# Deny loopback and private ranges
denied-peer-ip=10.0.0.0-10.255.255.255
denied-peer-ip=192.168.0.0-192.168.255.255
denied-peer-ip=172.16.0.0-172.31.255.255
denied-peer-ip=127.0.0.0-127.255.255.255

# Allow only specific ports
min-port=49152
max-port=65535

EOF

# Создание самоподписанных сертификатов
echo "🔐 Создание SSL сертификатов..."
mkdir -p /etc/ssl/certs /etc/ssl/private

# Генерация приватного ключа
openssl genrsa -out /etc/ssl/private/turn-server.key 2048

# Генерация самоподписанного сертификата
openssl req -new -x509 -key /etc/ssl/private/turn-server.key \
    -out /etc/ssl/certs/turn-server.crt -days 365 -subj \
    "/C=RU/ST=Moscow/L=Moscow/O=RobotClient/OU=TURN/CN=193.169.240.11"

# Установка правильных прав доступа
chmod 600 /etc/ssl/private/turn-server.key
chmod 644 /etc/ssl/certs/turn-server.crt
chown turnserver:turnserver /etc/ssl/private/turn-server.key /etc/ssl/certs/turn-server.crt

# Включение и запуск coturn
echo "🚀 Запуск TURN-сервера..."
systemctl enable coturn
systemctl start coturn

# Открытие портов в файрволе
echo "🔓 Настройка файрвола..."
ufw allow 3478/udp comment "TURN UDP"
ufw allow 3478/tcp comment "TURN TCP"
ufw allow 5349/tcp comment "TURNS TLS"
ufw allow 49152:65535/udp comment "TURN relay ports"

# Проверка статуса
echo "✅ Проверка статуса TURN-сервера..."
systemctl status coturn --no-pager

# Тестирование TURN сервера
echo "🧪 Тестирование TURN сервера..."
netstat -tuln | grep -E "(3478|5349)"

echo ""
echo "✅ TURN-сервер установлен и настроен!"
echo "📋 Конфигурация:"
echo "   🌐 Сервер: 193.169.240.11"
echo "   🔌 UDP порт: 3478"
echo "   🔐 TLS порт: 5349"
echo "   👤 Пользователь: robotclient"
echo "   🔑 Пароль: robotclient2024"
echo ""
echo "📊 Мониторинг:"
echo "   📄 Логи: tail -f /var/log/turnserver.log"
echo "   📈 Статус: systemctl status coturn"
echo "   🔄 Рестарт: systemctl restart coturn"
echo ""
echo "🌍 Для тестирования с клиента:"
echo "   curl http://193.169.240.11:80/api/turn/stats"
echo "" 