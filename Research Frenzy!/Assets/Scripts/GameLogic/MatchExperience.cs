// RESEARCHER XP (new file)
// MatchExperience - how many experience points (XP) a researcher gets for a study, by how the player matched it.
// The same shape as MatchRewards (in StudyScoring.cs): [Serializable], so it shows up as a foldout on
// FirmController in the Inspector ("Match Experience") and the numbers can be tuned there without code.
//
//   right approach + best method   -> 2 XP
//   right approach + good method   -> 1 XP
//   wrong approach + best method   -> 1 XP
//   wrong approach + good method   -> 1 XP
//   anything with a wrong method   -> 0 XP
//
// The XP is given when the player COLLECTS the finished study (FirmController.CollectResearch), the same moment
// the money is paid and the researcher becomes free again.

using System;
using Researchers;
using UnityEngine;

namespace GameLogic
{
    [Serializable]
    public class MatchExperience
    {
        [Tooltip("Approach right, methodology best")]
        [Min(0)] public int approachRightMethodBest = 2;
        [Tooltip("Approach right, methodology good")]
        [Min(0)] public int approachRightMethodGood = 1;
        [Tooltip("Approach right, methodology incorrect")]
        [Min(0)] public int approachRightMethodWrong = 0;
        [Tooltip("Approach wrong, methodology best")]
        [Min(0)] public int approachWrongMethodBest = 1;
        [Tooltip("Approach wrong, methodology good")]
        [Min(0)] public int approachWrongMethodGood = 1;
        [Tooltip("Approach wrong, methodology incorrect")]
        [Min(0)] public int approachWrongMethodWrong = 0;

        /// <summary>The XP for a graded match. Same switch as MatchRewards.For.</summary>
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
    /// One level up, sent by FirmController.ResearcherLeveledUp. DaySummary lists these at the end of the day.
    /// If a researcher jumps two levels at once, one of these is sent for each level.
    /// </summary>
    public class ResearcherLevelUp
    {
        public ResearcherData Researcher;
        public int NewLevel;
        public int UnlockedMethodologyId;   // the methodology this level unlocked, 0 if they don't know one for this slot
    }
}
