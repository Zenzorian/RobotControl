#!/usr/bin/env node

console.log('🔍 Проверка WebRTC возможностей...');

// Проверяем доступность WebRTC библиотек
let webrtcAvailable = false;
let webrtcLibrary = null;

// Пытаемся загрузить различные WebRTC библиотеки
const webrtcLibraries = [
  { name: 'wrtc', package: 'wrtc' },
  { name: 'node-webrtc', package: 'node-webrtc' },
  { name: '@koush/wrtc', package: '@koush/wrtc' }
];

for (const lib of webrtcLibraries) {
  try {
    require.resolve(lib.package);
    webrtcLibrary = lib;
    webrtcAvailable = true;
    console.log(`✅ Найдена WebRTC библиотека: ${lib.name}`);
    break;
  } catch (error) {
    // Библиотека не найдена
  }
}

if (!webrtcAvailable) {
  console.log('⚠️  WebRTC библиотеки не найдены');
  console.log('📋 Для полной WebRTC поддержки установите одну из библиотек:');
  console.log('   npm install wrtc');
  console.log('   npm install node-webrtc');
  console.log('   npm install @koush/wrtc');
  console.log('');
  console.log('🎯 СИГНАЛИНГ СЕРВЕР будет работать без MediaPeer (только сигналы)');
  console.log('   Это нормально для большинства случаев использования');
}

// Создаем файл конфигурации WebRTC
const fs = require('fs');
const path = require('path');

const webrtcConfig = {
  available: webrtcAvailable,
  library: webrtcLibrary,
  signalingOnly: !webrtcAvailable,
  checked: new Date().toISOString()
};

const configPath = path.join(__dirname, '../src/config/webrtc-config.json');
fs.writeFileSync(configPath, JSON.stringify(webrtcConfig, null, 2));

console.log(`📝 Конфигурация WebRTC сохранена: ${configPath}`);
console.log('🚀 Сервер готов к запуску с WebRTC сигналингом!'); 