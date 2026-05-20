mergeInto(LibraryManager.library, {
  LTSS_InitClipboardBridge: function (gameObjectNamePtr, callbackMethodNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    var callbackMethodName = UTF8ToString(callbackMethodNamePtr);

    if (!window.LTSS_ClipboardBridge) {
      window.LTSS_ClipboardBridge = {
        enabled: false,
        target: "",
        callback: "",
        initialized: false
      };
    }

    var bridge = window.LTSS_ClipboardBridge;
    bridge.target = gameObjectName;
    bridge.callback = callbackMethodName;

    if (bridge.initialized) {
      return;
    }

    bridge.initialized = true;

    window.addEventListener(
      "paste",
      function (event) {
        var currentBridge = window.LTSS_ClipboardBridge;

        if (!currentBridge || !currentBridge.enabled || !currentBridge.target || !currentBridge.callback) {
          return;
        }

        var clipboardData = event.clipboardData || window.clipboardData;
        var text = clipboardData && typeof clipboardData.getData === "function"
          ? clipboardData.getData("text")
          : "";

        if (typeof text !== "string" || text.length === 0) {
          return;
        }

        SendMessage(currentBridge.target, currentBridge.callback, text);
        event.preventDefault();
      },
      true
    );
  },

  LTSS_SetClipboardBridgeEnabled: function (enabled) {
    if (!window.LTSS_ClipboardBridge) {
      return;
    }

    window.LTSS_ClipboardBridge.enabled = !!enabled;
  }
});
