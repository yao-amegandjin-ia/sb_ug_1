// PreviousSessionCheck - handles a session that never ended properly (the tab was killed, the game crashed,
// the power went out). Nothing can be logged at that moment, so the next time the game starts, GameLog calls
// this to add a "session_interrupted" line to the end of the old session's file.
//
// ===== WITH AN API OR DATABASE =====
// The server could do this itself: any session with no session_end after a while counts as interrupted.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DataLogging
{
    internal static class PreviousSessionCheck
    {
        public static void MarkInterrupted(IDataLogSink sink, string oldSessionId, string playerId)
        {
            try
            {
                // read the old file's last line, to carry on its numbering and say when it really stopped
                string path = JsonFileLogSink.GetSessionFile(oldSessionId);
                int lastSeq = 0, lastDay = 0;
                string lastTs = null, lastType = null;

                if (File.Exists(path))
                {
                    string lastLine = File.ReadAllLines(path).LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                    if (lastLine != null)
                    {
                        JObject last = JObject.Parse(lastLine);
                        lastSeq = last.Value<int?>("seq") ?? 0;
                        lastTs = last.Value<string>("ts_utc");
                        lastType = last.Value<string>("type");
                        lastDay = last.Value<int?>("day") ?? 0;
                    }
                }

                sink.Write(new LogEvent
                {
                    EventId = Guid.NewGuid().ToString("N"),
                    SessionId = oldSessionId,
                    PlayerId = playerId,
                    Seq = lastSeq + 1,
                    TsUtc = LogHelpers.IsoNow(),   // when it was noticed, NOT when it happened. last_event_ts is the better guess
                    T = -1,
                    Category = "session",
                    Type = "session_interrupted",
                    Day = lastDay,
                    BuildVersion = Application.version,
                    Data = new Dictionary<string, object>
                    {
                        ["reason"] = "no_session_end",
                        ["last_event_ts"] = lastTs,
                        ["last_event_type"] = lastType,
                        ["detected_at_startup"] = true,
                    },
                });
                Debug.Log($"GameLog: session {oldSessionId} never ended properly, marked it as interrupted.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameLog: couldn't mark session {oldSessionId} as interrupted ({e.Message}).");
            }
        }
    }
}
