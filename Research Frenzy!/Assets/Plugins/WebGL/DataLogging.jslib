// DataLogging.jslib - browser-side helpers for the data logger. Only used in WebGL builds.
// Must stay in a folder named Plugins/WebGL (Unity picks up .jslib files there automatically).
// The C# side is Scripts/DataLogging/DataLogWebGL.cs.

mergeInto(LibraryManager.library, {

  // Unity keeps persistentDataPath files in memory and copies them to IndexedDB later.
  // This asks for the copy right now, so a closed tab loses as little as possible.
  DataLog_SyncFiles: function () {
    try {
      FS.syncfs(false, function (err) {
        if (err) console.warn("DataLog: saving logs to IndexedDB failed", err);
      });
    } catch (e) {
      console.warn("DataLog: FS.syncfs isn't available", e);
    }
  },

  // Calls back into C# when the tab is hidden (1), visible again (2), or being closed / navigated away (3).
  DataLog_RegisterPageEvents: function (callback) {
    var send = function (code) {
      try { {{{ makeDynCall('vi', 'callback') }}}(code); }
      catch (e) { console.warn("DataLog: page event callback failed", e); }
    };
    document.addEventListener("visibilitychange", function () {
      send(document.visibilityState === "hidden" ? 1 : 2);
    });
    window.addEventListener("pagehide", function () { send(3); });
  },

  // Hands the player a text file to save (the logs).
  DataLog_Download: function (fileNamePtr, textPtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var text = UTF8ToString(textPtr);
    var blob = new Blob([text], { type: "application/x-ndjson" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }
});
