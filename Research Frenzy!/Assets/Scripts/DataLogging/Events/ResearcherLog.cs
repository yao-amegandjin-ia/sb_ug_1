// ResearcherLog - the FIRM events about researchers: hiring one, a researcher getting XP, and a level up.
// Same pattern as DayLog / ClientLog: each function wraps GameLog.Emit in GameLog.Safe, so a logging problem
// can never break the game.
//
//   HIRE in the shop (not the free starter) -> Hired            (researcher_hired)
//   a study is collected                    -> ExperienceGained (researcher_xp)
//   that XP reached a new level             -> LeveledUp        (researcher_level_up, once per level)
//
// Called from FirmController (Hire, and GiveExperience inside CollectResearch).

using System.Collections.Generic;
using Clients;
using Researchers;

namespace DataLogging
{
    public static class ResearcherLog
    {
        /// <summary>A researcher was hired from the shop and paid for. FirmController.Hire.</summary>
        public static void Hired(ResearcherData researcher, int cost, int moneyLeft)
        {
            GameLog.Safe(() =>
            {
                if (researcher == null) return;
                GameLog.Emit("firm", "researcher_hired", new Dictionary<string, object>
                {
                    ["researcher_id"] = researcher.Id,
                    ["researcher_name"] = researcher.Name,
                    ["cubicle"] = researcher.Position,
                    ["cost"] = cost,
                    ["money_left"] = moneyLeft,
                    ["speed"] = researcher.Speed,
                    ["quality"] = researcher.Quality,
                    ["level"] = researcher.Level,
                });
            });
        }

        /// <summary>
        /// A collected study gave the researcher XP (logged even when it's 0, so every study has a row).
        /// FirmController.GiveExperience, right after ResearcherData.FinishAssignment added it.
        /// </summary>
        public static void ExperienceGained(ResearcherData researcher, ClientData client, int gained, int levelBefore)
        {
            GameLog.Safe(() =>
            {
                if (researcher == null) return;
                GameLog.Emit("firm", "researcher_xp", new Dictionary<string, object>
                {
                    ["researcher_id"] = researcher.Id,
                    ["researcher_name"] = researcher.Name,
                    ["client_id"] = client?.Id,
                    ["question_id"] = client?.RequestId,
                    ["xp_gained"] = gained,
                    ["xp_total"] = researcher.Experience,
                    ["level_before"] = levelBefore,
                    ["level_after"] = researcher.Level,
                });
            });
        }

        /// <summary>The researcher reached a new level and unlocked a methodology.</summary>
        public static void LeveledUp(ResearcherData researcher, int newLevel, int unlockedMethodologyId)
        {
            GameLog.Safe(() =>
            {
                if (researcher == null) return;
                GameLog.Emit("firm", "researcher_level_up", new Dictionary<string, object>
                {
                    ["researcher_id"] = researcher.Id,
                    ["researcher_name"] = researcher.Name,
                    ["new_level"] = newLevel,
                    ["xp_total"] = researcher.Experience,
                    ["unlocked_methodology_id"] = unlockedMethodologyId,
                    ["unlocked_methodology_name"] = GameDataObjects.MethodLibrary.GetName(unlockedMethodologyId),
                });
            });
        }
    }
}
