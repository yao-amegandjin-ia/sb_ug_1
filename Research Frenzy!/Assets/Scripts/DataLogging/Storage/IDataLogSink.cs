// IDataLogSink - where logged events GO. GameLog builds the events, a sink stores them.
// Right now the only sink is JsonFileLogSink (one JSON Lines file per play session, in a DataLogs folder).
//
// ===== SWAPPING IN AN API OR A DATABASE LATER =====
// Nothing else in the game needs to change. Write a new class that implements this interface, for example:
//
//     public class ApiLogSink : IDataLogSink
//     {
//         // Write(): add the event to a List<LogEvent> buffer (don't send one web request per event)
//         // Flush(): POST the buffer as a JSON array with UnityWebRequest to e.g. /sessions/{session_id}/events,
//         //          and only clear the buffer once the server says 200. Keep a JsonFileLogSink as a backup
//         //          so nothing is lost when the server can't be reached.
//         // event_id is unique, so the server can ignore an event it already has if a batch is sent twice.
//     }
//
// or a database sink that does INSERT INTO events (event_id, session_id, seq, ts_utc, type, data_json ...).
// Then change the ONE line in GameLog.CreateSink() to return your new sink. CompositeLogSink (below) lets
// you use two at once, e.g. the file AND the API.
//
// A sink should never throw into the game: catch and Debug.LogWarning instead. Losing a log line is bad,
// but crashing the player's game because the log failed is worse.

using System.Collections.Generic;

namespace DataLogging
{
    public interface IDataLogSink
    {
        /// <summary>Stores one event. Called on the main thread. Should be quick (buffer it, don't block).</summary>
        void Write(LogEvent logEvent);

        /// <summary>
        /// Makes sure everything written so far is actually saved (to disk / IndexedDB / the server).
        /// force = true for important moments (end of session, end of a quiz, tab hidden), where it must happen now.
        /// force = false is the regular timer, and a sink may skip it if it saved very recently.
        /// </summary>
        void Flush(bool force);

        /// <summary>Where the data ends up, for the Console message at startup (a folder path, a URL...).</summary>
        string Describe();
    }

    /// <summary>Sends every event to several sinks, e.g. the JSON files AND an API. Not used yet.</summary>
    public class CompositeLogSink : IDataLogSink
    {
        private readonly List<IDataLogSink> sinks;

        public CompositeLogSink(params IDataLogSink[] sinks)
        {
            this.sinks = new List<IDataLogSink>(sinks);
        }

        public void Write(LogEvent logEvent)
        {
            foreach (IDataLogSink sink in sinks) sink.Write(logEvent);
        }

        public void Flush(bool force)
        {
            foreach (IDataLogSink sink in sinks) sink.Flush(force);
        }

        public string Describe()
        {
            List<string> parts = new List<string>();
            foreach (IDataLogSink sink in sinks) parts.Add(sink.Describe());
            return string.Join(" + ", parts);
        }
    }
}
