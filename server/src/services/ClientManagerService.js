const WebSocket = require('ws');

class ClientManagerService {
  constructor() {
    this.clients = {
      controller: null,
      robot: null
    };
  }

  registerClient(ws, clientType) {
    if (clientType === 'CONTROLLER') {
      this.clients.controller = ws;
      ws.clientType = 'controller';
      console.log('🎮 Контроллер зарегистрирован');
      return 'REGISTERED!CONTROLLER';
    } else if (clientType === 'ROBOT') {
      this.clients.robot = ws;
      ws.clientType = 'robot';
      console.log('🤖 Робот зарегистрирован');
      return 'REGISTERED!ROBOT';
    }
    
    return null;
  }

  unregisterClient(ws) {
    if (ws.clientType === 'controller') {
      console.log('❌ Контроллер отключился');
      this.clients.controller = null;
    } else if (ws.clientType === 'robot') {
      console.log('❌ Робот отключился');
      this.clients.robot = null;
    }
  }

  getTargetClient(fromClientType) {
    if (fromClientType === 'controller') {
      return this.clients.robot;
    } else if (fromClientType === 'robot') {
      return this.clients.controller;
    }
    return null;
  }

  isClientConnected(clientType) {
    const client = this.clients[clientType];
    return client && client.readyState === WebSocket.OPEN;
  }

  getConnectionsStatus() {
    return {
      controller: this.clients.controller ? 'connected' : 'disconnected',
      robot: this.clients.robot ? 'connected' : 'disconnected'
    };
  }

  sendToClient(clientType, message) {
    const client = this.clients[clientType];
    if (client && client.readyState === WebSocket.OPEN) {
      client.send(message);
      return true;
    }
    return false;
  }

  sendToTarget(fromClientType, message) {
    const targetClient = this.getTargetClient(fromClientType);
    if (targetClient && targetClient.readyState === WebSocket.OPEN) {
      targetClient.send(message);
      return true;
    }
    return false;
  }
}

module.exports = ClientManagerService; 