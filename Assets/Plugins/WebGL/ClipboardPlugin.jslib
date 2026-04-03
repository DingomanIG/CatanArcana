mergeInto(LibraryManager.library, {
  WebGL_GetClipboardText: function (callbackObj, callbackMethod) {
    var objStr = UTF8ToString(callbackObj);
    var methodStr = UTF8ToString(callbackMethod);
    navigator.clipboard.readText().then(function (text) {
      SendMessage(objStr, methodStr, text);
    }).catch(function (err) {
      console.warn("Clipboard read failed: " + err);
      SendMessage(objStr, methodStr, "");
    });
  }
});
