// JsonFileLogSink - the current way logs are stored: one file per play session, one JSON object per line
// (a format called "JSON Lines", .jsonl), in a DataLogs folder of its own, away from the save data.
//
// Why one object per line instead of one big JSON array: the game can be closed at any moment. A big array
// is only valid JSON once its closing ] is written, so an interrupted session would leave a broken file.
// With one object per line, every line already written is complete and readable, whatever happens after it.
// Python (pandas.read_json(path, lines=True)), R, DuckDB and most databases read this format directly.
//
// Where the folder is:
//   Editor / Windows build:  Application.persistentDataPath/DataLogs
//                            (e.g. C:\Users\<you>\AppData\LocalLow\<Company>\<Product>\DataLogs)
//   WebGL build:             inside the browser's IndexedDB (Unity maps persistentDataPath there).
//                            The player can't open that folder, so LogDownload.DownloadLogs() hands the files over
//                            as a download. See the Data Logging Guide.
//
// WebGL detail: Unity writes files to an in-memory copy first and copies them to IndexedDB afterwards.
// Flush() asks the browser to do that copy now (DataLogWebGL.SyncFiles), so a closed tab loses as little as possible.
//
// ===== REPLACING THIS WITH AN API OR DATABASE =====
// See IDataLogSink.cs. This class can stay as an offline backup next to the new sink.

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace DataLogging
{
    public class JsonFileLogSink : IDataLogSink
    {
        public const string FolderName = "DataLogs";
        public const string FileExtension = ".jsonl";

        // WebGL only: don't ask the browser to copy to IndexedDB more often than this (a normal flush, not a forced one)
        private const float MinSecondsBetweenSyncs = 2f;

        private readonly string folder;
        private bool unsyncedChanges;
        private float lastSyncTime = -999f;

        public JsonFileLogSink()
        {
            folder = GetFolder();
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"JsonFileLogSink: couldn't create {folder} ({e.Message}). Events won't be saved.");
            }
        }

        /// <summary>The DataLogs folder. Static so the logger state and the download button can find it too.</summary>
        public static string GetFolder()
        {
            return Path.Combine(Application.persistentDataPath, FolderName);
        }

        /// <summary>The log file for one session.</summary>
        public static string GetSessionFile(string sessionId)
        {
            return Path.Combine(GetFolder(), "session_" + sessionId + FileExtension);
        }

        public void Write(LogEvent logEvent)
        {
            try
            {
                // written straight away (one line per event): in the Editor or a desktop build it's on disk right now,
                // in WebGL it's in the in-memory copy until the next Flush
                File.AppendAllText(GetSessionFile(logEvent.SessionId), logEvent.ToJson() + "\n", Encoding.UTF8);
                unsyncedChanges = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"JsonFileLogSink: couldn't write a {logEvent.Type} event ({e.Message}).");
            }
        }

        public void Flush(bool force)
        {
            if (!unsyncedChanges) return;
            if (!force && Time.realtimeSinceStartup - lastSyncTime < MinSecondsBetweenSyncs) return;

            DataLogWebGL.SyncFiles();   // does nothing outside WebGL, where the file is already on disk
            unsyncedChanges = false;
            lastSyncTime = Time.realtimeSinceStartup;
        }

        public string Describe()
        {
            return folder;
        }
    }
}
