mergeInto(LibraryManager.library, {

  /**
   * Show a native HTML <input> overlay on top of the Unity canvas.
   * Handles IME (Korean, Japanese, Chinese) and all keyboard input natively.
   *
   * @param {number} ptrObjName   - Unity GameObject name (UTF8)
   * @param {number} ptrCallback  - C# method to receive final value (UTF8)
   * @param {number} ptrInitValue - Initial text value (UTF8)
   * @param {number} x            - Left position in CSS pixels
   * @param {number} y            - Top position in CSS pixels
   * @param {number} w            - Width in CSS pixels
   * @param {number} h            - Height in CSS pixels
   * @param {number} fontSize     - Font size in CSS pixels
   */
  WebGLTextInput_Show: function (ptrObjName, ptrCallback, ptrInitValue, x, y, w, h, fontSize) {
    var objName   = UTF8ToString(ptrObjName);
    var callback  = UTF8ToString(ptrCallback);
    var initValue = UTF8ToString(ptrInitValue);

    // Remove any existing overlay input
    var existing = document.getElementById('unity-text-input-overlay');
    if (existing) existing.remove();

    var canvas = document.getElementById('unity-canvas');
    if (!canvas) return;

    var rect = canvas.getBoundingClientRect();

    // Scale factor: canvas internal resolution vs CSS display size
    var scaleX = rect.width  / canvas.width;
    var scaleY = rect.height / canvas.height;

    var input = document.createElement('input');
    input.id = 'unity-text-input-overlay';
    input.type = 'text';
    input.value = initValue;
    input.autocomplete = 'off';
    input.setAttribute('autocorrect', 'off');
    input.setAttribute('autocapitalize', 'off');
    input.setAttribute('spellcheck', 'false');

    // Position over the TextField
    input.style.cssText = [
      'position: fixed',
      'left: '   + (rect.left + x * scaleX) + 'px',
      'top: '    + (rect.top  + y * scaleY) + 'px',
      'width: '  + (w * scaleX) + 'px',
      'height: ' + (h * scaleY) + 'px',
      'font-size: ' + (fontSize * scaleY) + 'px',
      'font-family: inherit',
      'color: #e8d5b5',
      'background: rgba(30, 24, 50, 0.95)',
      'border: 1px solid #7c5cbf',
      'border-radius: 4px',
      'padding: 0 8px',
      'outline: none',
      'z-index: 9999',
      'box-sizing: border-box',
      'caret-color: #e8d5b5'
    ].join('; ') + ';';

    document.body.appendChild(input);

    // Focus immediately
    setTimeout(function() { input.focus(); }, 0);

    // Send value back to Unity on Enter or blur
    function commitValue() {
      var val = input.value || '';
      input.remove();
      SendMessage(objName, callback, val);
    }

    input.addEventListener('keydown', function(e) {
      if (e.key === 'Enter') {
        e.preventDefault();
        commitValue();
      }
      if (e.key === 'Escape') {
        e.preventDefault();
        input.remove();
        // Send original value back (cancel)
        SendMessage(objName, callback, initValue);
      }
    });

    input.addEventListener('blur', function() {
      // Small delay to avoid race conditions with Enter key
      setTimeout(function() {
        if (document.getElementById('unity-text-input-overlay')) {
          commitValue();
        }
      }, 100);
    });
  },

  /**
   * Hide and remove the overlay input without committing.
   */
  WebGLTextInput_Hide: function () {
    var el = document.getElementById('unity-text-input-overlay');
    if (el) el.remove();
  }
});