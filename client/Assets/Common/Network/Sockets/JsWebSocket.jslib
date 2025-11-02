var LibraryJsWebSocket = {
    $jsWebSocketState: {
        handlers: {},        // Dictionary: { id: { id, url, ws } }
        lastId: 0,           // Counter for unique handler IDs
        onOpen: null,        // Global callback for connection opened
        onMessage: null,     // Global callback for message received
        onError: null,       // Global callback for error occurred
        onClose: null,       // Global callback for connection closed
        debug: false          // Enable debug logging
    },

    // Set callback for connection opened event
    JsWebSocketSetOnOpen: function (callback) {
        jsWebSocketState.onOpen = callback;
    },

    // Set callback for message received event
    JsWebSocketSetOnMessage: function (callback) {
        jsWebSocketState.onMessage = callback;
    },

    // Set callback for error occurred event
    JsWebSocketSetOnError: function (callback) {
        jsWebSocketState.onError = callback;
    },

    // Set callback for connection closed event
    JsWebSocketSetOnClose: function (callback) {
        jsWebSocketState.onClose = callback;
    },

    // Create new WebSocket handler and return unique ID
    JsWebSocketCreate: function (urlPtr) {
        const url = UTF8ToString(urlPtr);
        const id = jsWebSocketState.lastId++;

        jsWebSocketState.handlers[id] = {
            id: id,
            url: url,
            ws: null
        };

        if (jsWebSocketState.debug) {
            console.log("[JsWebSocket] Handler created: id=" + id + ", url=" + url);
        }

        return id;
    },

    // Connect WebSocket handler by ID
    JsWebSocketConnect: function (id) {
        const handler = jsWebSocketState.handlers[id];
        if (!handler) {
            console.error("[JsWebSocket] Handler not found: id=" + id);
            return -1;
        }

        if (handler.ws !== null) {
            console.error("[JsWebSocket] Handler already connected: id=" + id);
            return -2;
        }

        try {
            handler.ws = new WebSocket(handler.url);
            handler.ws.binaryType = 'arraybuffer';

            // Setup event handlers
            handler.ws.onopen = function () {
                if (jsWebSocketState.debug) {
                    console.log("[JsWebSocket] Connected: id=" + id);
                }

                if (jsWebSocketState.onOpen) {
                    const func = Module['asm']['__indirect_function_table'].get(jsWebSocketState.onOpen);
                    func(id);
                }
            };

            handler.ws.onmessage = function (event) {
                if (jsWebSocketState.debug) {
                    console.log("[JsWebSocket] Message received: id=" + id + ", size=" + event.data.byteLength);
                }

                if (!jsWebSocketState.onMessage) return;

                // Convert data to byte array
                let dataBuffer;
                if (event.data instanceof ArrayBuffer) {
                    dataBuffer = new Uint8Array(event.data);
                } else if (event.data instanceof Blob) {
                    // Handle Blob type (shouldn't happen with binaryType='arraybuffer')
                    console.warn("[JsWebSocket] Received Blob data, converting...");
                    return;
                } else {
                    // Text message - convert to UTF8 bytes
                    dataBuffer = new TextEncoder().encode(event.data);
                }

                // Allocate memory in WASM and copy data
                const buffer = _malloc(dataBuffer.length);
                HEAPU8.set(dataBuffer, buffer);

                try {
                    const func = Module['asm']['__indirect_function_table'].get(jsWebSocketState.onMessage);
                    func(id, buffer, dataBuffer.length);
                } finally {
                    _free(buffer);
                }
            };

            handler.ws.onerror = function (event) {
                if (jsWebSocketState.debug) {
                    console.error("[JsWebSocket] Error occurred: id=" + id);
                }

                if (jsWebSocketState.onError) {
                    const msg = "WebSocket error occurred";
                    const length = lengthBytesUTF8(msg) + 1;
                    const buffer = _malloc(length);
                    stringToUTF8(msg, buffer, length);

                    try {
                        const func = Module['asm']['__indirect_function_table'].get(jsWebSocketState.onError);
                        func(id, buffer);
                    } finally {
                        _free(buffer);
                    }
                }
            };

            handler.ws.onclose = function (event) {
                if (jsWebSocketState.debug) {
                    console.log("[JsWebSocket] Closed: id=" + id + ", code=" + event.code + ", reason=" + event.reason);
                }

                if (jsWebSocketState.onClose) {
                    const func = Module['asm']['__indirect_function_table'].get(jsWebSocketState.onClose);
                    func(id, event.code);
                }

                handler.ws = null;
            };

            return 0;
        } catch (error) {
            console.error("[JsWebSocket] Failed to connect: id=" + id + ", error=" + error);
            return -3;
        }
    },

    // Send binary data through WebSocket
    JsWebSocketSend: function (id, bufferPtr, length) {
        const handler = jsWebSocketState.handlers[id];
        if (!handler) {
            console.error("[JsWebSocket] Handler not found: id=" + id);
            return -1;
        }

        if (!handler.ws) {
            console.error("[JsWebSocket] WebSocket not connected: id=" + id);
            return -2;
        }

        if (handler.ws.readyState !== WebSocket.OPEN) {
            console.error("[JsWebSocket] WebSocket not ready: id=" + id + ", state=" + handler.ws.readyState);
            return -3;
        }

        try {
            // Copy data from WASM memory
            const buffer = HEAPU8.slice(bufferPtr, bufferPtr + length);
            handler.ws.send(buffer);

            if (jsWebSocketState.debug) {
                console.log("[JsWebSocket] Sent: id=" + id + ", size=" + length);
            }

            return 0;
        } catch (error) {
            console.error("[JsWebSocket] Failed to send: id=" + id + ", error=" + error);
            return -4;
        }
    },

    // Close WebSocket connection
    JsWebSocketClose: function (id, code, reasonPtr) {
        const handler = jsWebSocketState.handlers[id];
        if (!handler) {
            console.error("[JsWebSocket] Handler not found: id=" + id);
            return -1;
        }

        if (!handler.ws) {
            console.warn("[JsWebSocket] WebSocket already closed: id=" + id);
            return 0;
        }

        try {
            const reason = reasonPtr ? UTF8ToString(reasonPtr) : "Normal closure";
            handler.ws.close(code || 1000, reason);

            if (jsWebSocketState.debug) {
                console.log("[JsWebSocket] Closing: id=" + id + ", code=" + code + ", reason=" + reason);
            }

            return 0;
        } catch (error) {
            console.error("[JsWebSocket] Failed to close: id=" + id + ", error=" + error);
            return -2;
        }
    },

    // Free WebSocket handler resources
    JsWebSocketFree: function (id) {
        const handler = jsWebSocketState.handlers[id];
        if (!handler) {
            console.warn("[JsWebSocket] Handler not found for free: id=" + id);
            return 0;
        }

        // Close connection if still open
        if (handler.ws && handler.ws.readyState < WebSocket.CLOSING) {
            handler.ws.close(1000, "Handler freed");
        }

        delete jsWebSocketState.handlers[id];

        if (jsWebSocketState.debug) {
            console.log("[JsWebSocket] Handler freed: id=" + id);
        }

        return 0;
    },

    // Get WebSocket connection state
    JsWebSocketGetState: function (id) {
        const handler = jsWebSocketState.handlers[id];
        if (!handler) {
            return -1; // Handler not found
        }

        if (!handler.ws) {
            return WebSocket.CLOSED; // 3
        }

        return handler.ws.readyState;
    }
};

// Register dependencies and merge into library
autoAddDeps(LibraryJsWebSocket, '$jsWebSocketState');
mergeInto(LibraryManager.library, LibraryJsWebSocket);
