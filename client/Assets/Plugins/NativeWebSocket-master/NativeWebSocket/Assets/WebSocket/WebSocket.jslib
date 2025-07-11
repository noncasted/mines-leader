var LibraryWebSocket = {
  $webSocketState: {
    instances: {},   // { id: { url, ws, subprotocols } }
    lastId: 0,
    onOpen: null,
    onMessage: null,
    onError: null,
    onClose: null,
    debug: false
  },

  WebSocketSetOnOpen: function(callback) {
    webSocketState.onOpen = callback;
  },

  WebSocketSetOnMessage: function(callback) {
    webSocketState.onMessage = callback;
  },

  WebSocketSetOnError: function(callback) {
    webSocketState.onError = callback;
  },

  WebSocketSetOnClose: function(callback) {
    webSocketState.onClose = callback;
  },

  WebSocketAllocate: function(url) {
    const urlStr = UTF8ToString(url);
    const id = webSocketState.lastId++;
    webSocketState.instances[id] = {
      url: urlStr,
      ws: null,
      subprotocols: []
    };
    return id;
  },

  WebSocketAddSubProtocol: function(instanceId, subprotocol) {
    const subprotocolStr = UTF8ToString(subprotocol);
    webSocketState.instances[instanceId].subprotocols.push(subprotocolStr);
  },

  WebSocketFree: function(instanceId) {
    const instance = webSocketState.instances[instanceId];
    if (!instance) return 0;

    if (instance.ws && instance.ws.readyState < 2) {
      instance.ws.close();
    }

    delete webSocketState.instances[instanceId];
    return 0;
  },

  WebSocketConnect: function(instanceId) {
    const instance = webSocketState.instances[instanceId];
    if (!instance || instance.ws !== null) return -1;

    instance.ws = new WebSocket(instance.url, instance.subprotocols);
    instance.ws.binaryType = 'arraybuffer';

    instance.ws.onopen = function () {
      if (webSocketState.debug) console.log("[WebSocket] Connected");

      if (webSocketState.onOpen) {
        const func = Module['asm']['__indirect_function_table'].get(webSocketState.onOpen);
        func(instanceId);
      }
    };

    instance.ws.onmessage = function (ev) {
      if (webSocketState.debug) console.log("[WebSocket] Message received", ev.data);

      if (!webSocketState.onMessage) return;

      let dataBuffer;
      if (ev.data instanceof ArrayBuffer) {
        dataBuffer = new Uint8Array(ev.data);
      } else {
        dataBuffer = new TextEncoder().encode(ev.data);
      }

      const buffer = _malloc(dataBuffer.length);
      HEAPU8.set(dataBuffer, buffer);

      try {
        const func = Module['asm']['__indirect_function_table'].get(webSocketState.onMessage);
        func(instanceId, buffer, dataBuffer.length);
      } finally {
        _free(buffer);
      }
    };

    instance.ws.onerror = function () {
      if (webSocketState.debug) console.log("[WebSocket] Error occurred");

      if (webSocketState.onError) {
        const msg = "WebSocket error.";
        const length = lengthBytesUTF8(msg) + 1;
        const buffer = _malloc(length);
        stringToUTF8(msg, buffer, length);

        try {
          const func = Module['asm']['__indirect_function_table'].get(webSocketState.onError);
          func(instanceId, buffer);
        } finally {
          _free(buffer);
        }
      }
    };

    instance.ws.onclose = function (ev) {
      if (webSocketState.debug) console.log("[WebSocket] Closed");

      if (webSocketState.onClose) {
        const func = Module['asm']['__indirect_function_table'].get(webSocketState.onClose);
        func(instanceId, ev.code);
      }

      delete instance.ws;
    };

    return 0;
  },

  WebSocketClose: function(instanceId, code, reasonPtr) {
    const instance = webSocketState.instances[instanceId];
    if (!instance || !instance.ws) return -1;

    if (instance.ws.readyState === 2) return -4;
    if (instance.ws.readyState === 3) return -5;

    const reason = reasonPtr ? UTF8ToString(reasonPtr) : undefined;

    try {
      instance.ws.close(code, reason);
    } catch (err) {
      return -7;
    }

    return 0;
  },

  WebSocketSend: function(instanceId, bufferPtr, length) {
    const instance = webSocketState.instances[instanceId];
    if (!instance || !instance.ws) return -1;
    if (instance.ws.readyState !== 1) return -6;

    const buffer = HEAPU8.slice(bufferPtr, bufferPtr + length);
    instance.ws.send(buffer);
    return 0;
  },

  WebSocketSendText: function(instanceId, messagePtr) {
    const instance = webSocketState.instances[instanceId];
    if (!instance || !instance.ws) return -1;
    if (instance.ws.readyState !== 1) return -6;

    const message = UTF8ToString(messagePtr);
    instance.ws.send(message);
    return 0;
  },

  WebSocketGetState: function(instanceId) {
    const instance = webSocketState.instances[instanceId];
    if (!instance) return -1;
    return instance.ws ? instance.ws.readyState : 3;
  }
};

autoAddDeps(LibraryWebSocket, '$webSocketState');
mergeInto(LibraryManager.library, LibraryWebSocket);
