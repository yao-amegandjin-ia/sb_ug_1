// LogTypes - the small shared types the log uses: why a session or day ended, and what's in the save at startup.

namespace DataLogging
{
    /// <summary>Why a session ended. Used as session_end's "reason" and the interrupted lines' "interrupted_by".</summary>
    public static class EndReason
    {
        public const string SaveAndQuit = "save_and_quit";   // call GameLog.EndSession(EndReason.SaveAndQuit) from a Save & Quit button
        public const string AppQuit = "app_quit";            // Editor Stop button / desktop build closed
        public const string PageClosed = "page_closed";      // WebGL: tab closed, refreshed or navigated away
        public const string GameComplete = "game_complete";  // finished the last day
    }

    /// <summary>Why a day ended. Used as day_end's "reason".</summary>
    public static class DayEndReason
    {
        public const string Timer = "timer";                     // the day timer ran out
        public const string NoClientsLeft = "no_clients_left";   // every client for the day has come in
        public const string Failed = "failed";                   // not in the game yet. Passing this interrupts open clients
    }

    /// <summary>What GameInitializer knows about the save when the game starts.</summary>
    public class SessionInfo
    {
        public int Seed;
        public string SaveSource;          // "api" or "offline"
        public int Day;
        public int Money;
        public int TotalMoneyEarned;
        public int RequestsAnswered;
        public int UpgradesOwned;
        public int ResearchersHired;
    }
}
