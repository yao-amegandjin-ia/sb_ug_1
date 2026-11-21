// RESEARCH TIMER (new file)
// ResearchSession - one client working with one researcher, from MATCH until the player collects the result.
// FirmController keeps one of these per client that is with a researcher (or waiting to be collected).
// The grade and the money are worked out when MATCH is pressed and kept here, so collecting later just pays
// what was already decided. The timer itself lives in ClientData (like the patience timer) and is ticked by Client.
//
// The two rules B asked for are the two functions at the bottom:
//   speed   -> Duration: higher speed = shorter session
//   quality -> Reward:   higher quality = more money
// Speed uses the helper that was already in ResearcherData (GetAdjustedDuration).
// Quality (changed Nov 21) is NOT a straight multiplier any more: each level above 1 adds a bonus,
// set on FirmController as "Quality Bonus Per Level" (0.25): quality 1 = x1, 2 = x1.25, 3 = x1.5.
// ResearcherData.GetAdjustedPayout (base x quality) is no longer used for this.

using GameDataObjects;
using Researchers;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// One research session: who, what was picked, how it was graded and what it will pay.
    /// A plain class (not a MonoBehaviour), it's only data that FirmController holds on to.
    /// </summary>
    public class ResearchSession
    {
        public Client Client;                 // the body in the lab (the client stands next to the researcher)
        public Researcher Researcher;         // the researcher's body. Can become null if they're removed
        public ResearcherData ResearcherData; // kept separately so the data is still there if the body is gone
        public ResearchApproach Approach;     // step one pick
        public int MethodologyId;             // step two pick
        public MatchGrade Grade;              // graded at MATCH time
        public int BaseReward;                // from the MatchRewards table, before quality
        public int Reward;                    // after quality. 0 stays 0, so a wrong match still pays nothing
        public float Duration;                // seconds, after speed

        /// <summary>
        /// How long a session with this researcher lasts. baseSeconds is FirmController's "Base Research Seconds" (20).
        /// ResearcherData.GetAdjustedDuration divides by speed: speed 1 = 20s, speed 2 = 10s, speed 3 = about 6.7s.
        /// </summary>
        public static float DurationFor(ResearcherData researcher, float baseSeconds)
        {
            return researcher != null ? researcher.GetAdjustedDuration(baseSeconds) : baseSeconds;
        }

        // RESEARCH TIMER (changed, Nov 21): was base x quality (x1 / x2 / x3)
        /// <summary>
        /// How much the researcher's quality multiplies the money: 1 + bonusPerLevel for every level above 1.
        /// With bonusPerLevel 0.25: quality 1 = 1, quality 2 = 1.25, quality 3 = 1.5.
        /// </summary>
        public static float QualityMultiplier(ResearcherData researcher, float bonusPerLevel)
        {
            int quality = researcher != null ? researcher.Quality : 1;
            return 1f + bonusPerLevel * Mathf.Max(0, quality - 1);
        }

        /// <summary>
        /// The money for a match after quality, rounded to whole dollars. $100 at quality 2 = $125, at quality 3 = $150.
        /// 0 stays 0, so a wrong match still pays nothing.
        /// </summary>
        public static int RewardFor(int baseReward, float qualityMultiplier)
        {
            if (baseReward <= 0) return 0;
            // FloorToInt(x + 0.5) always rounds .5 up ($62.50 -> $63). Mathf.RoundToInt would round .5 to the nearest even number
            return Mathf.FloorToInt(baseReward * qualityMultiplier + 0.5f);
        }
    }
}
