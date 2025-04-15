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

# Генерируем приватный ключ
echo "Генерация приватного ключа..."
openssl genrsa -out ssl/private.key 2048 || {
    echo "Ошибка: Не удалось сгенерировать приватный ключ"
    exit 1
}

# Генерируем CSR (Certificate Signing Request)
echo "Генерация CSR..."
openssl req -new -key ssl/private.key -out ssl/certificate.csr -subj "/C=UA/ST=Kyiv/L=Kyiv/O=RobotControl/CN=robotcontrol.local" || {
    echo "Ошибка: Не удалось сгенерировать CSR"
    exit 1
}

# Генерируем самоподписанный сертификат
echo "Генерация самоподписанного сертификата..."
openssl x509 -req -days 365 -in ssl/certificate.csr -signkey ssl/private.key -out ssl/certificate.crt || {
    echo "Ошибка: Не удалось сгенерировать сертификат"
    exit 1
}

# Устанавливаем правильные права доступа
echo "Установка прав доступа..."
chmod 600 ssl/private.key || {
    echo "Ошибка: Не удалось установить права на приватный ключ"
    exit 1
}
chmod 644 ssl/certificate.crt || {
    echo "Ошибка: Не удалось установить права на сертификат"
    exit 1
}

echo "SSL-сертификаты успешно сгенерированы в директории ssl/"
echo "Приватный ключ: ssl/private.key"
echo "Сертификат: ssl/certificate.crt" 