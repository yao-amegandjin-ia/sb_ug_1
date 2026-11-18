// DayLog - the DAY events: a day starting, ending (and why), and the end-of-day save.
// Called from FirmController (StartDay, EndDay, SaveAfterDay).

using System.Collections.Generic;

namespace DataLogging
{
    public static class DayLog
    {
        public static void Started(int day, float dayLength, int clientsPlanned)
        {
            GameLog.Safe(() => GameLog.Emit("day", "day_start", new Dictionary<string, object>
            {
                ["day"] = day,
                ["day_length_seconds"] = LogHelpers.Round(dayLength),
                ["clients_planned"] = clientsPlanned,
            }, force: true));
        }

        /// <summary>
        /// The day ended. reason is one of DayEndReason. Clients still in the lab are listed (they stay for tomorrow).
        /// With DayEndReason.Failed they're logged as interrupted instead, since a failed day throws them out.
        /// </summary>
        public static void Ended(int day, string reason, int money, int totalMoneyEarned)
        {
            GameLog.Safe(() =>
            {
                GameLog.Emit("day", "day_end", new Dictionary<string, object>
                {
                    ["day"] = day,
                    ["reason"] = reason,
                    ["money"] = money,
                    ["total_money_earned"] = totalMoneyEarned,
                    ["clients_still_in_lab"] = ClientVisits.Snapshot(),
                });

                if (reason == DayEndReason.Failed) GameLog.InterruptOpenItems("day_failed");
                GameLog.Flush(true);
            });
        }

        /// <summary>The end-of-day save finished (or failed).</summary>
        public static void Saved(int day, bool success, string error)
        {
            GameLog.Safe(() => GameLog.Emit("day", "game_saved", new Dictionary<string, object>
            {
                ["day"] = day,
                ["success"] = success,
                ["error"] = error,
            }));
        }
    }
}
