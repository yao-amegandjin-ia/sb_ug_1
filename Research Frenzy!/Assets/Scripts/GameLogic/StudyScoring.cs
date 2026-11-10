using GameDataObjects;

namespace GameLogic
{
    /// <summary>How well a methodology fits a request. The numbers match HUD.AddStudyResult (0 best, 1 partial, 2 wrong).</summary>
    public enum StudyOutcome
    {
        Best = 0,
        Good = 1,
        Wrong = 2
    }

    /// <summary>Everything that can happen to a client, for the day summary.</summary>
    public enum ClientOutcome
    {
        Best,
        Good,
        Wrong,
        CorrectReject,   // "We can't help" on a request that can't be researched
        WrongReject,     // "We can't help" on a request that could have been researched
        Lost             // left before the player helped them
    }

    /// <summary>
    /// What happened with one client: their request, the methodology picked (0 when turned away or lost)
    /// and the outcome. FirmController sends one of these every time a client is finished.
    /// It carries the data itself, so listeners don't have to dig it out of the saved study results.
    /// </summary>
    public class ClientResult
    {
        public RequestData Request;
        public int MethodologyId;
        public ClientOutcome Outcome;

        public static ClientOutcome FromStudy(StudyOutcome outcome)
        {
            switch (outcome)
            {
                case StudyOutcome.Best: return ClientOutcome.Best;
                case StudyOutcome.Good: return ClientOutcome.Good;
                default: return ClientOutcome.Wrong;
            }
        }
    }

    /// <summary>
    /// The rules for grading the player's answers. Plain functions with no Unity objects,
    /// so FirmController, a results screen or a test can all use them.
    /// </summary>
    public static class StudyScoring
    {
        /// <summary>
        /// Grades step two: the methodology (an ID from methodologies.csv) the player matched a request with.
        /// </summary>
        public static StudyOutcome Score(RequestData request, int methodologyId)
        {
            if (request == null || !request.IsResearchable || methodologyId <= 0) return StudyOutcome.Wrong;
            if (methodologyId == request.BestMethodologyId) return StudyOutcome.Best;
            if (request.GoodMethodologyIds != null && request.GoodMethodologyIds.Contains(methodologyId)) return StudyOutcome.Good;
            return StudyOutcome.Wrong;
        }

        /// <summary>
        /// Step one: true if the player picked the right approach (quantitative / qualitative / mixed).
        /// Not used by the match screen yet. It's here so the data can be checked now.
        /// </summary>
        public static bool IsCorrectApproach(RequestData request, ResearchApproach picked)
        {
            return request != null && request.IsResearchable && picked != ResearchApproach.None && picked == request.Approach;
        }

        /// <summary>True when "We can't help" was the right call, because the request can't be researched.</summary>
        public static bool IsCorrectReject(RequestData request)
        {
            return request != null && !request.IsResearchable;
        }
    }
}
