using System;
using GameDataObjects;
using UnityEngine;

namespace GameLogic
{
    /// <summary>How well a methodology fits a request (step two). The numbers match HUD.AddStudyResult (0 best, 1 partial, 2 wrong).</summary>
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
    /// The grade for one match: was step one (the approach) right, and how good was step two (the methodology).
    /// A struct because it's two small values that always travel together.
    /// </summary>
    public struct MatchGrade
    {
        public bool ApproachCorrect;
        public StudyOutcome Method;

        public MatchGrade(bool approachCorrect, StudyOutcome method)
        {
            ApproachCorrect = approachCorrect;
            Method = method;
        }

        public override string ToString() => $"approach {(ApproachCorrect ? "right" : "wrong")}, method {Method}";
    }

    /// <summary>
    /// The payout table. [Serializable] makes it show up as a foldout in FirmController's Inspector,
    /// so the amounts can be tuned there without touching code. The defaults are the design's table.
    /// </summary>
    [Serializable]
    public class MatchRewards
    {
        [Tooltip("Approach right, methodology best")]
        public int approachRightMethodBest = 100;
        [Tooltip("Approach right, methodology good")]
        public int approachRightMethodGood = 50;
        [Tooltip("Approach right, methodology incorrect")]
        public int approachRightMethodWrong = 25;
        [Tooltip("Approach wrong, methodology best")]
        public int approachWrongMethodBest = 50;
        [Tooltip("Approach wrong, methodology good")]
        public int approachWrongMethodGood = 25;
        [Tooltip("Approach wrong, methodology incorrect")]
        public int approachWrongMethodWrong = 0;

        public int For(MatchGrade grade)
        {
            switch (grade.Method)
            {
                case StudyOutcome.Best: return grade.ApproachCorrect ? approachRightMethodBest : approachWrongMethodBest;
                case StudyOutcome.Good: return grade.ApproachCorrect ? approachRightMethodGood : approachWrongMethodGood;
                default: return grade.ApproachCorrect ? approachRightMethodWrong : approachWrongMethodWrong;
            }
        }
    }

    /// <summary>
    /// What happened with one client: their request, what the player picked (approach None and methodology 0
    /// when turned away or lost), the outcome and the money it paid. FirmController sends one of these every
    /// time a client is finished. It carries the data itself, so listeners don't have to dig it out of the save.
    /// </summary>
    public class ClientResult
    {
        public RequestData Request;
        public ResearchApproach Approach;   // step one pick. None for rejects and lost clients
        public bool ApproachCorrect;        // only means something for matches
        public int MethodologyId;
        public ClientOutcome Outcome;
        public int Reward;

        public bool IsMatch => Outcome == ClientOutcome.Best || Outcome == ClientOutcome.Good || Outcome == ClientOutcome.Wrong;

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
        /// <summary>Grades both steps of a match at once.</summary>
        public static MatchGrade Grade(RequestData request, ResearchApproach approach, int methodologyId)
        {
            return new MatchGrade(IsCorrectApproach(request, approach), Score(request, methodologyId));
        }

        /// <summary>
        /// Grades step two: the methodology (an ID from the methodologies spreadsheet) the player matched a request with.
        /// </summary>
        public static StudyOutcome Score(RequestData request, int methodologyId)
        {
            if (request == null || !request.IsResearchable || methodologyId <= 0) return StudyOutcome.Wrong;
            if (methodologyId == request.BestMethodologyId) return StudyOutcome.Best;
            if (request.GoodMethodologyIds != null && request.GoodMethodologyIds.Contains(methodologyId)) return StudyOutcome.Good;
            return StudyOutcome.Wrong;
        }

        /// <summary>Step one: true if the player picked the right approach (quantitative / qualitative / mixed).</summary>
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
