namespace GameLogic
{
    // inside the namespace so this MethodologiesEnum wins over any old global copy still in the project
    using Assets.Scripts.GameDataObjects;

    /// <summary>How well a methodology fits a request. The numbers match HUD.AddStudyResult (0 best, 1 partial, 2 wrong).</summary>
    public enum StudyOutcome
    {
        Best = 0,
        Good = 1,
        Wrong = 2
    }

    /// <summary>
    /// The rules for grading the player's answers. Plain functions with no Unity objects,
    /// so FirmController, a results screen or a test can all use them.
    /// </summary>
    public static class StudyScoring
    {
        /// <summary>Grades the methodology the player matched a request with.</summary>
        public static StudyOutcome Score(RequestData request, MethodologiesEnum picked)
        {
            if (request == null || !request.IsResearchable) return StudyOutcome.Wrong;
            if (picked == request.BestMethodology) return StudyOutcome.Best;
            if (request.GoodMethodologies != null && request.GoodMethodologies.Contains(picked)) return StudyOutcome.Good;
            return StudyOutcome.Wrong;
        }

        /// <summary>True when "We can't help" was the right call, because the request can't be researched.</summary>
        public static bool IsCorrectReject(RequestData request)
        {
            return request != null && !request.IsResearchable;
        }
    }
}