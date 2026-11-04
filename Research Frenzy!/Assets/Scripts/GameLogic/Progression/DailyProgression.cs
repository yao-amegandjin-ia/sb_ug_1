using System;

namespace Assets.Scripts.GameLogic.Progression
{
    public enum DayPhase { NotStarted, AwaitingFirstClick, Playing, Saving, SaveFailed, Summary, Finished }

    /// <summary>Day state and timing, independent of UI and server transport.</summary>
    public sealed class DailyProgression
    {
        public const int TotalDays = 7;
        public int Day { get; private set; } = 1;
        public DayPhase Phase { get; private set; } = DayPhase.NotStarted;
        public float TimeLeft { get; private set; }
        public float TimeLimit { get; private set; }
        public bool IsUntimed => Day == 1;
        public bool CanInteract => Phase == DayPhase.AwaitingFirstClick || Phase == DayPhase.Playing;
        public bool CanContinue => Phase == DayPhase.Summary && Day < TotalDays;
        public bool CanEnd => CanInteract;
        public event Action Changed;
        public event Action<int> DayStarted;

        public static int BaseClients(int day) => 3 + 2 * (ValidateDay(day) - 1);
        public static float BaseSeconds(int day) => ValidateDay(day) == 1 ? 0 : 100 + 20 * (day - 2);
        private static int ValidateDay(int day)
        {
            if (day < 1 || day > TotalDays) throw new ArgumentOutOfRangeException(nameof(day));
            return day;
        }
        public void StartDay(int day, float extraSeconds)
        {
            if (Phase != DayPhase.NotStarted && !CanContinue)
                throw new InvalidOperationException("Finish and save this day before starting another.");
            Day = ValidateDay(day);
            TimeLimit = IsUntimed ? 0 : BaseSeconds(day) + Math.Max(0, extraSeconds);
            TimeLeft = TimeLimit;
            Phase = DayPhase.AwaitingFirstClick;
            DayStarted?.Invoke(Day);
            Changed?.Invoke();
        }
        public void FirstClientClicked()
        {
            if (Phase != DayPhase.AwaitingFirstClick) return;
            Phase = DayPhase.Playing;
            Changed?.Invoke();
        }
        /// <returns>True when the caller should end the day.</returns>
        public bool Tick(float deltaTime)
        {
            if (Phase != DayPhase.Playing || IsUntimed) return false;
            TimeLeft = Math.Max(0, TimeLeft - Math.Max(0, deltaTime));
            Changed?.Invoke();
            return TimeLeft <= 0;
        }
        public bool TryEndDay()
        {
            if (!CanEnd) return false;
            Phase = DayPhase.Saving;
            Changed?.Invoke();
            return true;
        }
        public bool TryRetrySave()
        {
            if (Phase != DayPhase.SaveFailed) return false;
            Phase = DayPhase.Saving;
            Changed?.Invoke();
            return true;
        }
        public void SaveCompleted()
        {
            if (Phase != DayPhase.Saving) throw new InvalidOperationException("No save is in progress.");
            Phase = Day == TotalDays ? DayPhase.Finished : DayPhase.Summary;
            Changed?.Invoke();
        }
        public void SaveFailed()
        {
            if (Phase != DayPhase.Saving) return;
            Phase = DayPhase.SaveFailed;
            Changed?.Invoke();
        }
    }
}
