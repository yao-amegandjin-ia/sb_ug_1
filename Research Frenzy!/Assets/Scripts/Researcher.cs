using System;
using System.Collections.Generic;
using UnityEngine;

/**
 * TODO: Does it need to be a unique ID for researcher? or can i just have it as a tuple of session ID and researcher.
 */

/// <summary>
/// The possible states a Researcher can be in. Available= Can be assigned to a client. Busy: Currently working with client
/// </summary>
public enum ResearcherState
{
    Available,
    Busy
}

/// <summary>
/// NPC that helps the client conduct research by providing methodologies.
/// Occupies a cubicle in the research firm. Empty position means not purchased
/// </summary>
public class Researcher : MonoBehaviour
{
    
    [Header("Identity")] [SerializeField] private string researcherName = "Unnamed";

    //we will go from 1-10, so starter id will be 0
    [SerializeField] private int id = 0;

    // x:head, y:torso, z:legs
    [SerializeField] private Vector3Int appearance = Vector3Int.zero;

    [Header("Stats")] [SerializeField, Min(0)]
    private int experience = 0;

    [Tooltip("Higher speed = research finishes quicker.")] [SerializeField, Min(1)]
    private int speed = 1;

    [Tooltip("Multiplier applied to the resulting money.")] [SerializeField, Min(1)]
    private int quality = 1;

    [Header("Knowledge")] [SerializeField] private List<int> knownMethodologyIds = new List<int>();

    [Header("Placement & State")] [Tooltip("Cubicle number. 0 = not purchased.")] [SerializeField, Min(0)]
    private int position = 0;

    [SerializeField] private ResearcherState currentState = ResearcherState.Available;

    //ID of client being assisted
    [SerializeField] private int currentClientId = 0;

    // Events
    
    public event Action<Researcher, int> AssignmentStarted; // (researcher, clientId)
    public event Action<Researcher, int> AssignmentFinished; // (researcher, clientId)
    public event Action<Researcher, int> Purchased; // (researcher, cubicle)

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

    /// <summary>
    /// Sets up a researcher's data in code (e.g. when generating from a hiring pool).
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

    public bool KnowsMethodology(int methodologyId) => knownMethodologyIds.Contains(methodologyId);
    public bool LearnMethodology(int methodologyId)
    {
        if (methodologyId==0 || KnowsMethodology(methodologyId)) return false;
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
        if (!IsAvailable || clientId==0) return false;
        currentClientId = clientId;
        currentState = ResearcherState.Busy;
        AssignmentStarted?.Invoke(this, clientId);
        return true;
    }
    /// <summary>Finishes the current assignment and returns the researcher to Available.</summary>
    public void FinishAssignment(int experienceGained = 0)
    {
        if (currentState != ResearcherState.Busy) return;

        int finishedClient = currentClientId;
        currentClientId = 0;
        currentState = ResearcherState.Available;
        AddExperience(experienceGained);
        AssignmentFinished?.Invoke(this, finishedClient);
    }

    //Helpers for attributes

    /// <summary>
    /// Duration of a task after applying this researcher's speed.
    /// Higher speed finishes quicker.
    /// </summary>
    public float GetAdjustedDuration(float baseDuration) => baseDuration / Mathf.Max(1, speed);

    /// <summary>Money payout after applying this researcher's quality multiplier.</summary>
    public float GetAdjustedPayout(float basePayout) => basePayout * quality;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep data consistent when editing in the Inspector.
        if (currentState == ResearcherState.Available) currentClientId =0;
        if (currentState == ResearcherState.Busy && currentClientId==0)
            currentState = ResearcherState.Available;
    }
#endif
}