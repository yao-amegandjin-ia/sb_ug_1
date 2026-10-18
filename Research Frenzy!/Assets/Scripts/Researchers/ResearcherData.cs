using System;
using System.Collections.Generic;
using UnityEngine;

namespace Researchers
{
    /// <summary>
    ///     The possible states a Researcher can be in. Available= Can be assigned to a client. Busy: Currently working with
    ///     client
    /// </summary>
    public enum ResearcherState
    {
        Available,
        Busy
    }

    /// <summary>
    ///     The purpose of this class is to have the data for a researcher.
    ///     This contains the basic data points and some game logic, but does NOT have the game object.
    ///     This is needed so that we can track what researchers exist, but they don't need to be
    ///     instantiated yet.
    ///     The Researcher.cs script houses a ResearcherData object, and that script will be associated with a prefab.
    ///     This way, we can generate the Data for researchers, but don't have to spawn them in until they are purchased.
    /// </summary>
    [Serializable]
    public class ResearcherData
    {
        // x:head, y:torso, z:legs
        [SerializeField] private Vector3Int appearance = Vector3Int.zero;

        //ID of client being assisted
        [SerializeField] private int currentClientId = 0;

        [SerializeField] private ResearcherState currentState = ResearcherState.Available;

        [Header("Stats")] [SerializeField] [Min(0)]
        private int experience = 0;

        //we will go from 1-10, so starter id will be 0
        [SerializeField] private int id = 0;

        [Header("Knowledge")] [SerializeField] private  List<int> knownMethodologyIds = new List<int>();

        [Header("Placement & State")] [Tooltip("Cubicle number. 0 = not purchased.")] [SerializeField] [Min(0)]
        private int position = 0;

        [Tooltip("Multiplier applied to the resulting money.")] [SerializeField] [Min(1)]
        private int quality = 1;

        [Header("Identity")] [SerializeField] private string researcherName = "Unnamed";

        [Tooltip("Higher speed = research finishes quicker.")] [SerializeField] [Min(1)]
        private int speed = 1;

        // Properties 
        public string Name => researcherName;
        public int Id => id;
        public Vector3Int Appearance => appearance;
        public int Experience => experience;
        public int Speed => speed;
        public int Quality => quality;
        public IReadOnlyList<int> KnownMethodologyIds => knownMethodologyIds;
        public int Position => position;
        public ResearcherState CurrentState => currentState;
        public int CurrentClientId => currentClientId;

        public bool IsPurchased => position != 0;
        public bool IsAvailable => IsPurchased && currentState == ResearcherState.Available;

        // Events
        public event Action<ResearcherData, int> AssignmentStarted; // (researcher, clientId)
        public event Action<ResearcherData, int> AssignmentFinished; // (researcher, clientId)
        public event Action<ResearcherData, int> Purchased; // (researcher, cubicle)

        /// <summary>
        ///     Sets up a researcher's data in code (e.g. when generating from a hiring pool).
        /// </summary>
        public void Init(int newId, string newName, Vector3Int newAppearance, int newExperience,
            int newSpeed, int newQuality, IEnumerable<int> methodologies = null)
        {
            id = newId;

            researcherName = newName;
            appearance = newAppearance;
            experience = Mathf.Max(0, newExperience);
            speed = Mathf.Max(1, newSpeed);
            quality = Mathf.Max(1, newQuality);

            knownMethodologyIds.Clear();
            if (methodologies != null)
                knownMethodologyIds.AddRange(methodologies);

            position = 0;
            currentState = ResearcherState.Available;
            currentClientId = 0;
        }

        /// <summary>Buys the researcher and places them in the given cubicle (must be >= 1).</summary>
        public bool Purchase(int cubicle)
        {
            if (IsPurchased || cubicle <= 0) return false;

            position = cubicle;
            Purchased?.Invoke(this, cubicle);
            return true;
        }

        //Methodologies

        public bool KnowsMethodology(int methodologyId)
        {
            return knownMethodologyIds.Contains(methodologyId);
        }

        public bool LearnMethodology(int methodologyId)
        {
            if (methodologyId == 0 || KnowsMethodology(methodologyId)) return false;
            knownMethodologyIds.Add(methodologyId);
            return true;
        }

        // Experience
        public void AddExperience(int amount)
        {
            if (amount > 0) experience += amount;
        }

        //Client interactions
        /// <summary>Begins assisting a client. Fails if not purchased or already busy.</summary>
        public bool AssignClient(int clientId)
        {
            if (!IsAvailable || clientId == 0) return false;
            currentClientId = clientId;
            currentState = ResearcherState.Busy;
            AssignmentStarted?.Invoke(this, clientId);
            return true;
        }

        /// <summary>Finishes the current assignment and returns the researcher to Available.</summary>
        public void FinishAssignment(int experienceGained = 0)
        {
            if (currentState != ResearcherState.Busy) return;

            var finishedClient = currentClientId;
            currentClientId = 0;
            currentState = ResearcherState.Available;
            AddExperience(experienceGained);
            AssignmentFinished?.Invoke(this, finishedClient);
        }

        //Helpers for attributes

        /// <summary>
        ///     Duration of a task after applying this researcher's speed.
        ///     Higher speed finishes quicker.
        /// </summary>
        public float GetAdjustedDuration(float baseDuration)
        {
            return baseDuration / Mathf.Max(1, speed);
        }

        /// <summary>Money payout after applying this researcher's quality multiplier.</summary>
        public float GetAdjustedPayout(float basePayout)
        {
            return basePayout * quality;
        }
        
        /// <summary>
        /// Independent copy (own methodology list, no event subscribers). Used so saved
        /// snapshots can't be changed by later gameplay, and loaded data isn't shared with the save.
        /// </summary>
        public ResearcherData Clone()
        {
            ResearcherData copy = (ResearcherData)MemberwiseClone();
            copy.knownMethodologyIds = new List<int>(knownMethodologyIds);
            copy.AssignmentStarted = null;
            copy.AssignmentFinished = null;
            copy.Purchased = null;
            return copy;
        }

    }
    
    
    /// <summary>The whole researcher pool (hired and unhired) for a save file.</summary>
    [Serializable]
    public class ResearcherList
    {
        public List<ResearcherData> researchers = new List<ResearcherData>();
    }

}