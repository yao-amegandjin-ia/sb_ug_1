// LogDownload - gets the log files out of the game. Needed for WebGL, where the DataLogs folder is inside the
// browser's storage and the player can't open it. DataLogDownloadButton calls this.

using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DataLogging
{
    public static class LogDownload
    {
        /// <summary>
        /// WebGL: the browser downloads the logs as one .jsonl file. Editor: prints where the folder is.
        /// allSessions false = only this session's file.
        /// </summary>
        public static void DownloadLogs(bool allSessions = true)
        {
            try
            {
                GameLog.Flush(true);
                string folder = JsonFileLogSink.GetFolder();
                if (!Directory.Exists(folder)) return;

                string[] files = allSessions || GameLog.SessionId == null
                    ? Directory.GetFiles(folder, "*" + JsonFileLogSink.FileExtension).OrderBy(f => f).ToArray()
                    : new[] { JsonFileLogSink.GetSessionFile(GameLog.SessionId) };

                StringBuilder text = new StringBuilder();
                foreach (string file in files)
                    if (File.Exists(file)) text.Append(File.ReadAllText(file));

                string name = allSessions
                    ? "research_frenzy_logs_all.jsonl"
                    : "research_frenzy_session_" + GameLog.SessionId + ".jsonl";
                DataLogWebGL.Download(name, text.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"LogDownload: couldn't download the logs ({e.Message}).");
            }
        }
    }
}
