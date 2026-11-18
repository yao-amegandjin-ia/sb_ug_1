// DataLogWebGL - the C# side of Assets/Plugins/WebGL/DataLogging.jslib (small JavaScript functions that only
// exist in a WebGL build). Outside WebGL (the Editor, a Windows build) every function here does nothing,
// so the rest of the logging code never needs an #if.
//
//  SyncFiles():          copy Unity's in-memory files to IndexedDB now, so they survive the tab closing
//  RegisterPageEvents(): tell us when the tab is hidden, shown again, or closed. OnApplicationQuit doesn't run
//                        in a browser, so this is how a closed tab still gets logged
//  Download():           give the player a file to save (used by LogDownload.DownloadLogs)

using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

namespace DataLogging
{
    public static class DataLogWebGL
    {
        // the codes the jslib sends to the callback
        public const int PageHidden = 1;
        public const int PageVisible = 2;
        public const int PageClosing = 3;

        private static Action<int> pageEventHandler;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void DataLog_SyncFiles();
        [DllImport("__Internal")] private static extern void DataLog_RegisterPageEvents(Action<int> callback);
        [DllImport("__Internal")] private static extern void DataLog_Download(string fileName, string text);

        public static void SyncFiles() => DataLog_SyncFiles();

        public static void RegisterPageEvents(Action<int> handler)
        {
            pageEventHandler = handler;
            DataLog_RegisterPageEvents(OnPageEvent);
        }

        public static void Download(string fileName, string text) => DataLog_Download(fileName, text);

        // the browser calls this. It has to be static with this attribute, because WebGL builds can only call
        // static C# functions from JavaScript
        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnPageEvent(int code)
        {
            pageEventHandler?.Invoke(code);
        }
#else
        public static void SyncFiles() { }

        public static void RegisterPageEvents(Action<int> handler)
        {
            pageEventHandler = handler;   // never called outside WebGL. DataLogRunner uses OnApplicationQuit there instead
        }

        public static void Download(string fileName, string text)
        {
            Debug.Log($"DataLogWebGL: downloads only work in a WebGL build. In the Editor, open {JsonFileLogSink.GetFolder()}");
        }
#endif
    }
}
