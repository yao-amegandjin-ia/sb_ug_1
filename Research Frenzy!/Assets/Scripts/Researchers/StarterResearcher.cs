// StarterResearcher - one starting researcher the player can pick on the start screen.
// You edit these in the Inspector: ResearcherGenerator component -> "Starting Researchers" list.
// [Serializable] is what makes a plain class like this show up there as a foldout with its fields,
// the same trick as MatchRewards / MatchExperience on FirmController.
//
// Two scripts read the same list, so they can't disagree:
//   StartScreen         - draws one card per entry
//   ResearcherGenerator - makes the ResearcherData for each entry (ID = Researcher Count + 1 + position in the list)

using System;
using System.Collections.Generic;
using GameDataObjects;
using UnityEngine;

namespace Researchers
{
    [Serializable]
    public class StarterResearcher
    {
        [Tooltip("Shown on the start screen card, in the lab, everywhere.")]
        public string name = "New Starter";
        

        [Tooltip("Higher = research sessions finish quicker (20 s / speed).")]
        [Range(1, 3)] public int speed = 1;

        [Tooltip("Higher = studies pay more (x1 / x1.25 / x1.5).")]
        [Range(1, 3)] public int quality = 1;

        [Tooltip("IN UNLOCK ORDER. Element 0 is usable from the start, element 1 unlocks at level 2, 2 at level 3, 3 at level 4. " +
                 "Names from ResearchFrenzyMethodologies.csv (other names and ID numbers work too). " +
                 "Fewer than 4: the rest are picked at random when the game starts.")]
        public List<string> methodologies = new List<string>();

        [Tooltip("Which sprites the Researcher prefab shows. x = head, y = body, z = legs.")]
        public Vector3Int appearance = Vector3Int.zero;

        // a quick way to fill in the defaults in ResearcherGenerator
        public StarterResearcher() { }

        public StarterResearcher(string name,int speed, int quality, Vector3Int appearance, params string[] methodologies)
        {
            this.name = name;
            this.speed = speed;
            this.quality = quality;
            this.appearance = appearance;
            this.methodologies = new List<string>(methodologies);
        }

        /// <summary>
        /// The methodology names turned into IDs, in unlock order, using the same forgiving matching as the requests
        /// spreadsheet ("Field", "field research" and "2" all find Field Research). Names that don't match are left out,
        /// with a Console warning when warn is true. Duplicates are left out too.
        /// </summary>
        public List<int> GetMethodologyIds(bool warn = false)
        {
            List<int> ids = new List<int>();
            if (methodologies == null) return ids;

            foreach (string typed in methodologies)
            {
                if (string.IsNullOrWhiteSpace(typed)) continue;
                MethodEntry method = MethodLibrary.Find(typed);
                if (method == null)
                {
                    if (warn) Debug.LogWarning($"Starter {name}: methodology \"{typed}\" isn't in ResearchFrenzyMethodologies.csv. Left out.");
                }
                else if (!ids.Contains(method.id))
                {
                    ids.Add(method.id);
                }
            }
            return ids;
        }
    }
}
