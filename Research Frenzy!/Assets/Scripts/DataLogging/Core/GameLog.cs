// GameLog - the heart of the data log: it runs the session and writes every line.
// The events themselves are split by topic into their own classes (in DataLogging/Events):
//
//   GameLog    the session: start, save loaded, game start, end, tab hidden/closed       (this file)
//   DayLog     day start, day end, end-of-day save
//   ClientLog  a client's path through the lab (the process events)
//   AnswerLog  the player's answers about clients (the learning / performance events)
//   QuizLog    the mid and post quizzes
//
// They all build a Dictionary of fields and call GameLog.Emit, which wraps it in a LogEvent (timestamp, seq,
// day...) and hands it to the sink. The sink decides where it's stored: see CreateSink below and IDataLogSink.cs.
// Every event, its fields and where it's called from is in the Data Logging Guide (DATA_LOGGING.md).
//
// Logging must never break the game, so every event function runs inside GameLog.Safe, which catches errors.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Researchers;
using UnityEngine;

namespace DataLogging
{
    public static class GameLog
    {
        /// <summary>Set false to turn all logging off, e.g. for dev playtests that shouldn't be in the data.</summary>
        public static bool Enabled = true;

        // ======================================================================
        //  SWAP POINT: where the events go
        // ======================================================================

        private static IDataLogSink CreateSink()
        {
            // Today: JSON Lines files in the DataLogs folder.
            // Later, an API:       return new ApiLogSink("https://.../events");
            // Or both at once:     return new CompositeLogSink(new JsonFileLogSink(), new ApiLogSink(...));
            // Or a database:       return new DatabaseLogSink(connection);
            return new JsonFileLogSink();
        }

        // ======================================================================
        //  state
        // ======================================================================

        private static IDataLogSink sink;
        private static string sessionId;
        private static bool sessionOpen;
        private static string endedReason;
        private static int seq;
        private static double sessionStart;
        private static double pausedAt = -1;

        private static Func<int> dayProvider;
        private static Func<float> timeLeftProvider;

        public static bool IsSessionOpen => sessionOpen;
        public static string SessionId => sessionId;

        /// <summary>Player ID, quiz counts and the open-session marker. QuizLog uses it for all-time attempt counts.</summary>
        internal static LoggerState State { get; private set; }

        // ======================================================================
        //  session
        // ======================================================================

        /// <summary>
        /// Starts the log for this play session. GameInitializer calls it once the save has loaded.
        /// Also logs save_loaded, and closes off the previous session if it never ended properly.
        /// </summary>
        public static void StartSession(SessionInfo info)
        {
            if (!Enabled || sessionOpen) return;
            try
            {
                sink = CreateSink();
                State = LoggerState.Load();

                // the last session never logged session_end: the tab was killed, the game crashed, the power went out...
                string interruptedSession = State.OpenSessionId;
                if (!string.IsNullOrEmpty(interruptedSession))
                    PreviousSessionCheck.MarkInterrupted(sink, interruptedSession, State.PlayerId);

                sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" +
                            Guid.NewGuid().ToString("N").Substring(0, 6);
                seq = 0;
                sessionStart = LogHelpers.Now;
                sessionOpen = true;
                endedReason = null;
                ClientVisits.Clear();
                QuizLog.Reset();

                State.OpenSessionId = sessionId;
                State.SessionsStarted++;
                State.Save();

                DataLogRunner.EnsureExists();
                Debug.Log($"GameLog: logging session {sessionId} to {sink.Describe()}");

                Emit("session", "session_start", new Dictionary<string, object>
                {
                    ["platform"] = Application.platform.ToString(),
                    ["is_editor"] = Application.isEditor,
                    ["unity_version"] = Application.unityVersion,
                    ["seed"] = info?.Seed ?? 0,
                    ["sessions_started_on_this_device"] = State.SessionsStarted,
                    ["previous_session_interrupted"] = interruptedSession,
                });

                if (info != null) SaveLoaded(info);
                Flush(true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"GameLog: couldn't start the session log ({e.Message}). Playing without logging.");
                sessionOpen = false;
            }
        }

        /// <summary>
        /// A save was loaded. Logged at startup by StartSession. If a "Load game" button is ever added, call this
        /// when it loads: anything still open (clients, a quiz) is logged as interrupted by the load first.
        /// </summary>
        public static void SaveLoaded(SessionInfo info)
        {
            Safe(() =>
            {
                InterruptOpenItems("save_loaded");
                Emit("session", "save_loaded", new Dictionary<string, object>
                {
                    ["save_source"] = info.SaveSource,
                    ["loaded_existing_progress"] = info.Day > 1 || info.RequestsAnswered > 0 || info.TotalMoneyEarned > 0,
                    ["day"] = info.Day,
                    ["money"] = info.Money,
                    ["total_money_earned"] = info.TotalMoneyEarned,
                    ["requests_answered"] = info.RequestsAnswered,
                    ["upgrades_owned"] = info.UpgradesOwned,
                    ["researchers_hired"] = info.ResearchersHired,
                });
            });
        }

        /// <summary>The game started (Start Day 1 pressed, or a loaded game carried on).</summary>
        public static void GameStarted(bool pickedStarter, ResearcherData starter, IEnumerable<ResearcherData> hired)
        {
            Safe(() => Emit("session", "game_start", new Dictionary<string, object>
            {
                ["new_game"] = pickedStarter,
                ["starter_researcher_id"] = starter != null ? starter.Id : (object)null,
                ["starter_researcher_name"] = starter?.Name,
                ["hired_researcher_ids"] = hired?.Where(r => r != null).Select(r => r.Id).ToList() ?? new List<int>(),
            }));
        }

        /// <summary>
        /// Ends the session: logs every open client and quiz as interrupted, writes session_end and saves everything now.
        /// Called automatically when the game closes. A Save &amp; Quit button should call EndSession(EndReason.SaveAndQuit).
        /// </summary>
        public static void EndSession(string reason)
        {
            if (!sessionOpen) return;
            Safe(() =>
            {
                InterruptOpenItems(reason);
                Emit("session", "session_end", new Dictionary<string, object>
                {
                    ["reason"] = reason,
                    ["session_seconds"] = LogHelpers.Round(LogHelpers.Now - sessionStart),
                    ["events_logged"] = seq + 1,
                });
                Flush(true);
            });

            sessionOpen = false;
            endedReason = reason;
            if (State != null)
            {
                State.OpenSessionId = null;
                State.Save();
                DataLogWebGL.SyncFiles();   // so the state file reaches IndexedDB too
            }
        }

        /// <summary>
        /// Everything still open (clients in the lab, a quiz) gets an "interrupted" line.
        /// Used by EndSession, SaveLoaded and DayLog (failed day).
        /// </summary>
        internal static void InterruptOpenItems(string reason)
        {
            ClientVisits.InterruptAll(reason);
            QuizLog.Interrupt(reason);
        }

        /// <summary>FirmController hands over its day number and timer, so every event can say when in the game it happened.</summary>
        public static void AttachDayClock(Func<int> currentDay, Func<float> timeLeft)
        {
            dayProvider = currentDay;
            timeLeftProvider = timeLeft;
        }

        // ======================================================================
        //  writing events (used by every log class)
        // ======================================================================

        /// <summary>Wraps the fields in a LogEvent and sends it to the sink. force = save it right now.</summary>
        internal static void Emit(string category, string type, Dictionary<string, object> data, bool force = false)
        {
            if (!Enabled || !sessionOpen || sink == null) return;

            LogEvent e = new LogEvent
            {
                EventId = Guid.NewGuid().ToString("N"),
                SessionId = sessionId,
                PlayerId = State?.PlayerId,
                Seq = ++seq,
                TsUtc = LogHelpers.IsoNow(),
                T = LogHelpers.Round(LogHelpers.Now - sessionStart),
                Category = category,
                Type = type,
                Day = dayProvider != null ? dayProvider() : 0,
                DayTimeLeft = timeLeftProvider != null ? (float)LogHelpers.Round(timeLeftProvider()) : 0f,
                BuildVersion = Application.version,
                Data = data ?? new Dictionary<string, object>(),
            };
            sink.Write(e);
            if (force) sink.Flush(true);
        }

        internal static void Flush(bool force)
        {
            sink?.Flush(force);
        }

        /// <summary>Runs an event function, but only during a session, and never lets an error reach the game.</summary>
        internal static void Safe(Action action)
        {
            if (!Enabled || !sessionOpen) return;
            try { action(); }
            catch (Exception e) { Debug.LogWarning($"GameLog: an event couldn't be logged ({e.GetType().Name}: {e.Message})."); }
        }

        // ======================================================================
        //  called by DataLogRunner
        // ======================================================================

        /// <summary>Every frame: lets the sink save what's been written (it decides how often).</summary>
        internal static void Tick()
        {
            if (sink == null) return;
            try { sink.Flush(false); }
            catch (Exception e) { Debug.LogWarning($"GameLog: flush failed ({e.Message})."); }
        }

        /// <summary>WebGL: the tab was hidden, shown again, or closed (codes from DataLogWebGL).</summary>
        internal static void OnPageEvent(int code)
        {
            switch (code)
            {
                case DataLogWebGL.PageHidden:
                    // the most reliable warning a browser gives before a tab is closed, so save everything now
                    pausedAt = LogHelpers.Now;
                    Safe(() => Emit("session", "session_paused", new Dictionary<string, object>(), force: true));
                    break;

                case DataLogWebGL.PageVisible:
                    if (!sessionOpen && endedReason == EndReason.PageClosed)
                    {
                        // the browser brought the page back from its cache after "closing" it: carry on the same session
                        sessionOpen = true;
                        endedReason = null;
                        if (State != null) { State.OpenSessionId = sessionId; State.Save(); }
                        Safe(() => Emit("session", "session_reopened", new Dictionary<string, object>()));
                    }
                    Safe(() => Emit("session", "session_resumed", new Dictionary<string, object>
                    {
                        ["paused_seconds"] = LogHelpers.SecondsSince(pausedAt),
                    }));
                    pausedAt = -1;
                    break;

                case DataLogWebGL.PageClosing:
                    EndSession(EndReason.PageClosed);
                    break;
            }
        }
    }
}
