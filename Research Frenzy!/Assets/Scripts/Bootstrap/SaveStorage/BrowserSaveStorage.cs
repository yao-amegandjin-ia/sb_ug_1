// BrowserSaveStorage - keeps saves in the browser's localStorage. Used in WebGL builds.
// The JavaScript side is Assets/Plugins/WebGL/SaveStorage.jslib (same idea as DataLogging.jslib).
//
// localStorage is a small key -> text store every browser has, kept per website. It survives closing the tab and
// restarting the browser, and it's written straight away (no syncing like persistentDataPath needs in WebGL).
// It's cleared if the player clears their browser data, and private/incognito windows forget it when closed.
//
// Every name is stored with a prefix ("ResearchFrenzy/save.json") so it can't clash with anything else on the
// same website. Outside a WebGL build the JavaScript doesn't exist, so every function here throws instead.

using System;
using System.IO;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Bootstrap
{
    public sealed class BrowserSaveStorage : ISaveStorage
    {
        private readonly string prefix;

        public BrowserSaveStorage(string prefix = "ResearchFrenzy")
        {
            this.prefix = prefix + "/";
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // [DllImport("__Internal")] = "this function is in the jslib". The names must match the jslib exactly
        [DllImport("__Internal")] private static extern int SaveStorage_Exists(string key);
        [DllImport("__Internal")] private static extern string SaveStorage_Read(string key);
        [DllImport("__Internal")] private static extern int SaveStorage_Write(string key, string text);
        [DllImport("__Internal")] private static extern void SaveStorage_Delete(string key);

        public bool Exists(string name) => SaveStorage_Exists(prefix + name) == 1;

        public string Read(string name) => SaveStorage_Read(prefix + name);

        public void Write(string name, string text)
        {
            // 0 = the browser refused (storage full, or blocked by the browser's privacy settings)
            if (SaveStorage_Write(prefix + name, text) != 1)
                throw new IOException("The browser didn't let the game save (its storage is full or blocked).");
        }

        public void Delete(string name) => SaveStorage_Delete(prefix + name);
#else
        public bool Exists(string name) => throw NotHere();
        public string Read(string name) => throw NotHere();
        public void Write(string name, string text) => throw NotHere();
        public void Delete(string name) => throw NotHere();

        private static Exception NotHere() =>
            new PlatformNotSupportedException("BrowserSaveStorage only works in a WebGL build. Use FileSaveStorage.");
#endif

        public string Describe() => "browser localStorage (" + prefix + "...)";
    }
}
