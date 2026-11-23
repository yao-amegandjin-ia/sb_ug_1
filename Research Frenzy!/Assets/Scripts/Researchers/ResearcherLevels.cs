// RESEARCHER XP 
// ResearcherLevels - turns a researcher's experience points (XP) into a level, and a level into how many of
// their methodologies they can use. Plain C#, no Unity objects, so anything can ask (FirmController, the match
// screen, the hire tab, the manage firm panel, the data log).
//
//   level 1 at  0 XP -> 1 methodology unlocked (everyone starts here)
//   level 2 at  2 XP -> 2 methodologies
//   level 3 at  5 XP -> 3 methodologies
//   level 4 at 10 XP -> all 4 (the last level)
//
// To change the numbers, edit XpToReach below. To add a level 5, add a fifth number AND make sure
// researchers know 5 methodologies (ResearcherGenerator's "Methodologies Per Researcher").
// How much XP a study gives is NOT here, it's the "Match Experience" table on FirmController (MatchExperience.cs).

using UnityEngine;

namespace Researchers
{
    public static class ResearcherLevels
    {
        // XP needed to REACH each level. Index 0 is level 1, index 1 is level 2, and so on.
        // The first number must be 0 (level 1 needs no XP) and each number must be bigger than the one before.
        private static readonly int[] XpToReach = { 0, 2, 5, 10 };

        /// <summary>The highest level (4). Reaching it unlocks the last methodology.</summary>
        public static int MaxLevel => XpToReach.Length;

        /// <summary>The level for this much XP. 0-1 XP = 1, 2-4 = 2, 5-9 = 3, 10 or more = 4.</summary>
        public static int LevelFor(int experience)
        {
            int level = 1;
            for (int i = 1; i < XpToReach.Length; i++)
            {
                if (experience >= XpToReach[i]) level = i + 1;
                else break;   // the list is in order, so once one is too high the rest are too
            }
            return level;
        }

        /// <summary>XP needed to reach a level (level 2 -> 2). Levels below 1 count as 1, above the max as the max.</summary>
        public static int XpForLevel(int level)
        {
            int index = Mathf.Clamp(level, 1, MaxLevel) - 1;
            return XpToReach[index];
        }

        /// <summary>XP needed for the NEXT level, or -1 if this much XP is already the max level.</summary>
        public static int XpForNextLevel(int experience)
        {
            int level = LevelFor(experience);
            return level >= MaxLevel ? -1 : XpToReach[level];   // XpToReach[level] is the next level's number
        }

        /// <summary>How many methodologies a researcher at this level can use: one per level.</summary>
        public static int UnlockedMethodsAt(int level)
        {
            return Mathf.Clamp(level, 1, MaxLevel);
        }
    }
}
