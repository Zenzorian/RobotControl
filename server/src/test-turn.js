#!/usr/bin/env node

/**
 * TURN сервер тест - проверка доступности и функциональности
 */

const TurnConfig = require('./config/TurnConfig');
const net = require('net');

/**
 * Тестирование TCP подключения к TURN серверу
 */
async function testTurnConnection() {
    const host = TurnConfig.TURN_SERVER_HOST;
    const port = TurnConfig.TURN_SERVER_PORT;
    
    console.log(`🔍 Тестирование подключения к TURN серверу: ${host}:${port}`);
    
    return new Promise((resolve) => {
        const socket = new net.Socket();
        let connected = false;
        
        const timeout = setTimeout(() => {
            if (!connected) {
                socket.destroy();
                console.log(`❌ Таймаут подключения к ${host}:${port}`);
                resolve(false);
            }
        }, 5000);

        socket.connect(port, host, () => {
            connected = true;
            clearTimeout(timeout);
            console.log(`✅ Успешное подключение к ${host}:${port}`);
            socket.destroy();
            resolve(true);
        });

        socket.on('error', (error) => {
            connected = true; // Предотвращаем двойное срабатывание
            clearTimeout(timeout);
            console.log(`❌ Ошибка подключения к ${host}:${port}: ${error.message}`);
            resolve(false);
        });
    });
}

/**
 * Проверка разрешения имени
 */
async function testDNSResolution() {
    const dns = require('dns').promises;
    const host = TurnConfig.TURN_SERVER_HOST;
    
    // Если это IP адрес, пропускаем DNS тест
    if (net.isIP(host)) {
        console.log(`🔍 ${host} - это IP адрес, DNS тест пропущен`);
        return true;
    }
    
    try {
        console.log(`🔍 Проверка DNS для: ${host}`);
        const result = await dns.lookup(host);
        console.log(`✅ DNS разрешен: ${host} -> ${result.address}`);
        return true;
    } catch (error) {
        console.log(`❌ Ошибка DNS для ${host}: ${error.message}`);
        return false;
    }
}

/**
 * Тест ping для проверки достижимости хоста
 */
async function testPing() {
    const { spawn } = require('child_process');
    const host = TurnConfig.TURN_SERVER_HOST;
    
    console.log(`🔍 Ping тест для: ${host}`);
    
    return new Promise((resolve) => {
        // Определяем команду ping в зависимости от ОС
        const isWindows = process.platform === 'win32';
        const pingCmd = isWindows ? 'ping' : 'ping';
        const pingArgs = isWindows ? ['-n', '3', host] : ['-c', '3', host];
        
        const ping = spawn(pingCmd, pingArgs);
        let output = '';
        
        ping.stdout.on('data', (data) => {
            output += data.toString();
        });
        
        ping.stderr.on('data', (data) => {
            output += data.toString();
        });
        
        ping.on('close', (code) => {
            if (code === 0) {
                console.log(`✅ Ping успешен для ${host}`);
                // Извлекаем время отклика если возможно
                const timeMatch = output.match(/time[<=](\d+\.?\d*)ms/i);
                if (timeMatch) {
                    console.log(`   Время отклика: ${timeMatch[1]}ms`);
                }
                resolve(true);
            } else {
                console.log(`❌ Ping неудачен для ${host} (код: ${code})`);
                resolve(false);
            }
        });
        
        // Таймаут для ping
        setTimeout(() => {
            ping.kill();
            console.log(`❌ Ping таймаут для ${host}`);
            resolve(false);
        }, 10000);
    });
}

/**
 * Основная функция тестирования
 */
async function runTests() {
    console.log('🧪 TURN сервер диагностика\n');
    
    // Показываем конфигурацию
    TurnConfig.logDiagnostics();
    console.log('');
    
    const tests = [
        { name: 'DNS разрешение', fn: testDNSResolution },
        { name: 'Ping тест', fn: testPing },
        { name: 'TCP подключение', fn: testTurnConnection }
    ];
    
    let passed = 0;
    let total = tests.length;
    
    for (const test of tests) {
        console.log(`\n📋 Тест: ${test.name}`);
        const result = await test.fn();
        if (result) {
            passed++;
        }
    }
    
    console.log(`\n📊 РЕЗУЛЬТАТЫ ТЕСТИРОВАНИЯ:`);
    console.log(`   Пройдено: ${passed}/${total}`);
    console.log(`   Статус: ${passed === total ? '✅ ВСЕ ТЕСТЫ ПРОШЛИ' : '❌ ЕСТЬ ПРОБЛЕМЫ'}`);
    
    if (passed < total) {
        console.log(`\n🔧 РЕКОМЕНДАЦИИ:`);
        console.log(`   1. Проверьте запущен ли TURN сервер`);
        console.log(`   2. Проверьте настройки firewall`);
        console.log(`   3. Убедитесь что IP адрес доступен из сети`);
        console.log(`   4. Проверьте конфигурацию сети/роутера`);
    }
    
    process.exit(passed === total ? 0 : 1);
}

// Запускаем тесты
if (require.main === module) {
    runTests().catch(error => {
        console.error('❌ Критическая ошибка тестирования:', error);
        process.exit(1);
    });
}

module.exports = { testTurnConnection, testDNSResolution, testPing }; 