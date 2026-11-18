// LogEvent - one line in the data log.
// Every event has the same outer fields (the "envelope", below), so every line can be read the same way,
// and a "data" object with the fields for that kind of event (see the Data Logging Guide for each type).
//
// When this moves to a database, the envelope fields become the table's columns and "data" goes in a JSON column:
//     events(event_id PRIMARY KEY, session_id, player_id, seq, ts_utc, t, category, type, day, day_time_left,
//            build_version, schema_version, data JSON)

using System.Collections.Generic;
using Newtonsoft.Json;

namespace DataLogging
{
    public class LogEvent
    {
        /// <summary>Bump this when an event's fields change meaning, so old and new logs can be told apart.</summary>
        public const int SchemaVersion = 1;

        // ----- who and when -----

        [JsonProperty("event_id")] public string EventId;          // unique ID for this line (a GUID)
        [JsonProperty("session_id")] public string SessionId;      // one play session = one log file
        [JsonProperty("player_id")] public string PlayerId;        // anonymous ID, kept in this browser between sessions
        [JsonProperty("seq")] public int Seq;                      // 1, 2, 3... within the session. Sort by this
        [JsonProperty("ts_utc")] public string TsUtc;              // real clock time, UTC, ISO 8601 with milliseconds
        [JsonProperty("t")] public double T;                       // seconds since the session started (for durations)

        // ----- what -----

        [JsonProperty("category")] public string Category;         // session / day / process / learning / quiz
        [JsonProperty("type")] public string Type;                 // e.g. client_enter, question_answered, quiz_end

        // ----- where in the game -----

        [JsonProperty("day")] public int Day;                      // game day (0 before the lab has loaded)
        [JsonProperty("day_time_left")] public float DayTimeLeft;  // seconds left on the day timer

        [JsonProperty("build_version")] public string BuildVersion;   // Application.version (Player Settings > Version)
        [JsonProperty("schema_version")] public int Schema = SchemaVersion;

        // ----- the event's own fields -----

        [JsonProperty("data")] public Dictionary<string, object> Data;

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
}
