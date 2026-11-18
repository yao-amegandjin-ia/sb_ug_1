// LoggerState - the few things the logger has to remember between play sessions, kept in
// DataLogs/logger_state.json (next to the log files, not in the save):
//   - an anonymous player ID, so sessions from the same browser can be linked
//   - which session is still open, so a session that was cut off (tab closed, crash, power off)
//     gets a "session_interrupted" line the next time the game starts
//   - how many times each quiz has been taken, ever, on this device
//
// ===== WITH AN API OR DATABASE =====
// The server would keep these instead (player ID from the login, quiz counts from a COUNT(*) query),
// and this file can go. Until then it's per browser/computer: clearing site data resets it.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DataLogging
{
    public class LoggerState
    {
        [JsonProperty("player_id")] public string PlayerId;
        [JsonProperty("open_session_id")] public string OpenSessionId;     // null when the last session ended properly
        [JsonProperty("sessions_started")] public int SessionsStarted;
        [JsonProperty("quiz_attempts_all_time")] public Dictionary<string, int> QuizAttemptsAllTime = new Dictionary<string, int>();

        private static string FilePath => Path.Combine(JsonFileLogSink.GetFolder(), "logger_state.json");

        public static LoggerState Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    LoggerState loaded = JsonConvert.DeserializeObject<LoggerState>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        if (loaded.QuizAttemptsAllTime == null) loaded.QuizAttemptsAllTime = new Dictionary<string, int>();
                        if (string.IsNullOrEmpty(loaded.PlayerId)) loaded.PlayerId = Guid.NewGuid().ToString();
                        return loaded;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LoggerState: couldn't read {FilePath} ({e.Message}). Starting a new one.");
            }
            return new LoggerState { PlayerId = Guid.NewGuid().ToString() };
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(JsonFileLogSink.GetFolder());
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LoggerState: couldn't write {FilePath} ({e.Message}).");
            }
        }
    }
}
