// SaveStorage.jslib - SAVE GAME (new). Browser-side save storage, only used in WebGL builds.
// Must stay in a folder named Plugins/WebGL (Unity picks up .jslib files there automatically).
// The C# side is Scripts/Bootstrap/SaveStorage/BrowserSaveStorage.cs.
// localStorage can throw (private windows, storage turned off), so every call is wrapped in try/catch.

mergeInto(LibraryManager.library, {

  // 1 if something is saved under this key, 0 if not
  SaveStorage_Exists: function (keyPtr) {
    try {
      return window.localStorage.getItem(UTF8ToString(keyPtr)) !== null ? 1 : 0;
    } catch (e) {
      console.warn("SaveStorage: couldn't read localStorage", e);
      return 0;
    }
  },

  // the saved text, or null (0) if nothing is saved. Unity frees the memory after turning it into a C# string
  SaveStorage_Read: function (keyPtr) {
    var text = null;
    try {
      text = window.localStorage.getItem(UTF8ToString(keyPtr));
    } catch (e) {
      console.warn("SaveStorage: couldn't read localStorage", e);
    }
    if (text === null) return 0;
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  // 1 if it saved, 0 if the browser refused (full, or blocked)
  SaveStorage_Write: function (keyPtr, textPtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(textPtr));
      return 1;
    } catch (e) {
      console.warn("SaveStorage: couldn't write localStorage", e);
      return 0;
    }
  },

  SaveStorage_Delete: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
    } catch (e) {
      console.warn("SaveStorage: couldn't delete from localStorage", e);
    }
  }
});
