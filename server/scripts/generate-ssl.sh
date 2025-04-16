#!/bin/bash

# Проверка наличия OpenSSL
if ! command -v openssl &> /dev/null; then
    echo "Ошибка: OpenSSL не установлен"
    echo "Установите OpenSSL: sudo apt-get install openssl"
    exit 1
fi

# Создаем директорию для SSL-сертификатов
mkdir -p ssl || {
    echo "Ошибка: Не удалось создать директорию ssl/"
    exit 1
}

# Генерируем приватный ключ с повышенной безопасностью
echo "Генерация приватного ключа..."
openssl genrsa -aes256 -out ssl/private.key 4096 || {
    echo "Ошибка: Не удалось сгенерировать приватный ключ"
    exit 1
}

# Создаем конфигурационный файл для сертификата
cat > ssl/certificate.cnf << EOF
[req]
distinguished_name = req_distinguished_name
x509_extensions = v3_req
prompt = no

[req_distinguished_name]
C = UA
ST = Kyiv
L = Kyiv
O = RobotControl
OU = Development
CN = 193.169.240.11

[v3_req]
keyUsage = keyEncipherment, dataEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[alt_names]
IP.1 = 193.169.240.11
EOF

# Генерируем CSR (Certificate Signing Request)
echo "Генерация CSR..."
openssl req -new -key ssl/private.key -out ssl/certificate.csr -config ssl/certificate.cnf || {
    echo "Ошибка: Не удалось сгенерировать CSR"
    exit 1
}

# Генерируем самоподписанный сертификат с расширенными настройками
echo "Генерация самоподписанного сертификата..."
openssl x509 -req -days 365 -in ssl/certificate.csr -signkey ssl/private.key -out ssl/certificate.crt \
    -extensions v3_req -extfile ssl/certificate.cnf || {
    echo "Ошибка: Не удалось сгенерировать сертификат"
    exit 1
}

# Генерируем отпечаток сертификата
echo "Генерация отпечатка сертификата..."
openssl x509 -in ssl/certificate.crt -fingerprint -sha256 -noout > ssl/certificate.fingerprint || {
    echo "Ошибка: Не удалось сгенерировать отпечаток"
    exit 1
}

# Устанавливаем правильные права доступа
echo "Установка прав доступа..."
chmod 600 ssl/private.key || {
    echo "Ошибка: Не удалось установить права на приватный ключ"
    exit 1
}
chmod 644 ssl/certificate.crt ssl/certificate.fingerprint || {
    echo "Ошибка: Не удалось установить права на сертификат или отпечаток"
    exit 1
}

# Очищаем временные файлы
rm -f ssl/certificate.csr ssl/certificate.cnf

echo "SSL-сертификаты успешно сгенерированы в директории ssl/"
echo "Приватный ключ: ssl/private.key"
echo "Сертификат: ssl/certificate.crt"
echo "Отпечаток сертификата: ssl/certificate.fingerprint"
echo ""
echo "ВАЖНО: Сохраните отпечаток сертификата для настройки клиента!"
echo "Отпечаток сертификата:"
cat ssl/certificate.fingerprint 