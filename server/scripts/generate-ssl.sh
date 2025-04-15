#!/bin/bash

# Создаем директорию для SSL-сертификатов
mkdir -p ssl

# Генерируем приватный ключ
openssl genrsa -out ssl/private.key 2048

# Генерируем CSR (Certificate Signing Request)
openssl req -new -key ssl/private.key -out ssl/certificate.csr -subj "/C=UA/ST=Kyiv/L=Kyiv/O=RobotControl/CN=robotcontrol.local"

# Генерируем самоподписанный сертификат
openssl x509 -req -days 365 -in ssl/certificate.csr -signkey ssl/private.key -out ssl/certificate.crt

# Устанавливаем правильные права доступа
chmod 600 ssl/private.key
chmod 644 ssl/certificate.crt

echo "SSL-сертификаты успешно сгенерированы в директории ssl/" 