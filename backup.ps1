# Параметры подключения
$serverIP = "193.169.240.11"
$username = "ubuntu"
$password = "SQ4aQhx6u"
$backupPath = "H:\ServerBackup"
$date = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"

# Проверяем наличие plink
if (-not (Test-Path "C:\Program Files\PuTTY\plink.exe")) {
    Write-Host "Установите PuTTY для использования plink.exe"
    Write-Host "Скачайте с: https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html"
    exit
}

# Создаем директорию для бэкапа, если её нет
if (-not (Test-Path $backupPath)) {
    New-Item -ItemType Directory -Path $backupPath
}

# Создаем директорию с текущей датой
$currentBackupPath = Join-Path $backupPath $date
New-Item -ItemType Directory -Path $currentBackupPath

# Список важных директорий для бэкапа
$backupDirs = @(
    "/etc",                    # Системные конфигурации
    "/home",                   # Домашние директории пользователей
    "/var/www",               # Веб-сервер
    "/var/log",               # Логи
    "/opt",                   # Установленные программы
    "/root",                  # Конфигурации root
    "/usr/local",             # Локально установленные программы
    "/var/lib/docker"         # Docker контейнеры и данные
)

Write-Host "Начинаем полный бэкап сервера..."

# Функция для выполнения команд через plink
function Invoke-PlinkCommand {
    param (
        [string]$Command
    )
    $plinkCommand = "echo y | plink -ssh $username@$serverIP -pw $password `"$Command`""
    Invoke-Expression $plinkCommand
}

# Функция для копирования файлов через pscp
function Copy-FilesWithPscp {
    param (
        [string]$Source,
        [string]$Destination
    )
    $pscpCommand = "pscp -pw $password -r $username@${serverIP}:$Source $Destination"
    Invoke-Expression $pscpCommand
}

# Копируем каждую директорию
foreach ($dir in $backupDirs) {
    $targetDir = Join-Path $currentBackupPath $dir.Replace("/", "\")
    New-Item -ItemType Directory -Path $targetDir -Force
    Write-Host "Копируем $dir..."
    Copy-FilesWithPscp -Source "$dir/*" -Destination $targetDir
}

# Бэкап списка установленных пакетов
Write-Host "Создаем список установленных пакетов..."
Invoke-PlinkCommand "dpkg --get-selections > /tmp/installed_packages.txt"
Copy-FilesWithPscp -Source "/tmp/installed_packages.txt" -Destination $currentBackupPath

# Бэкап системной информации
Write-Host "Собираем системную информацию..."
Invoke-PlinkCommand "uname -a > /tmp/system_info.txt && df -h >> /tmp/system_info.txt"
Copy-FilesWithPscp -Source "/tmp/system_info.txt" -Destination $currentBackupPath

# Архивируем бэкап
Write-Host "Архивируем бэкап..."
Compress-Archive -Path $currentBackupPath -DestinationPath "$backupPath\full_backup_$date.zip"

# Удаляем временную директорию
Remove-Item -Recurse -Force $currentBackupPath

Write-Host "Полный бэкап сервера успешно создан: $backupPath\full_backup_$date.zip" 