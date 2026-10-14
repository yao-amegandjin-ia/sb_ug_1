// Client - one customer that walks into the lab.
// keeps their request info, patience timer and current state.
// the panel buttons call the public functions below to change what happens to them.

using System;
using UnityEngine;

public enum ClientState
{
    InProcess,        // just walked in, waiting at the door for the player to click them
    Waiting,          // sitting in the waiting area
    Assisted,         // in a consultation with a researcher
    Returning,        // told to come back tomorrow
    Completed,        // done (consultation finished or request rejected)
    LeftDissatisfied,  // patience ran out
}

// Request / Researcher objects aren't merged yet so those are placeholders for now
public class Client : MonoBehaviour
{
    [Header("Info")]
    public string company;
    public int reward; 
    [SerializeField] private int id = 0;
    // TODO: swap for the Request object once 3-requests_object is merged
    [TextArea] public string requestText;

    [SerializeField] public int requestId;
    
    [Header("Skin")]
    [SerializeField] private Vector3Int appearance = Vector3Int.zero;
    

    [Header("Patience")]
    public float maxPatience = 30f;
    public float currentPatience;

    [Header("Assignment")]
    // TODO: swap for the Researcher object once it's merged
    public int assignedResearcherId = -1;
    public int assignedMethodologyId = -1;

    [Header("State")]
    public ClientState state = ClientState.InProcess;

    // the game manager can listen to these
    public event Action<Client> OnStateChanged;
    public event Action<Client> OnLeftDissatisfied;
    

    void Awake()
    {
        currentPatience = maxPatience;
    }

    void Update()
    {
        // patience only drops while the client hasn't been helped yet
        // (opening the request panel does NOT pause it, per the GDD)
        if (state == ClientState.InProcess || state == ClientState.Waiting)
        {
            currentPatience -= Time.deltaTime;

            if (currentPatience <= 0f)
            {
                currentPatience = 0f;
                SetState(ClientState.LeftDissatisfied);
                OnLeftDissatisfied?.Invoke(this);
            }
        }
    }

    // called when the client is spawned
    public void Init(int id, string company, int requestId, int reward, float patience, Vector3Int appearance, string request)
    {
        this.id = id;
        this.company = company;
        this.requestId = requestId;
        this.reward = reward;
        maxPatience = patience;
        currentPatience = patience;
        assignedResearcherId = -1;
        assignedMethodologyId = -1;
        this.appearance = appearance;
        this.requestText = request;

        SetState(ClientState.InProcess);
    }
    

    // for the patience bar UI (0 to 1)
    public float GetPatiencePercent()
    {
        if (maxPatience <= 0f) return 0f;
        return currentPatience / maxPatience;
    }

    // "Ask them to wait"
    public void SendToWaitingRoom()
    {
        if (state == ClientState.InProcess || state == ClientState.Waiting)
            SetState(ClientState.Waiting);
    }

    // "Assign" - researcher + methodology picked on the request screen
    public void AssignTo(int researcherId, int methodologyId)
    {
        assignedResearcherId = researcherId;
        assignedMethodologyId = methodologyId;
        SetState(ClientState.Assisted);
    }

    // "Ask them to come back tomorrow"
    public void AskToReturnTomorrow()
    {
        SetState(ClientState.Returning);
    }

    // returning client walks back in the next day (keeps same skin + request)
    public void Reenter()
    {
        currentPatience = maxPatience;
        assignedResearcherId = -1;
        assignedMethodologyId = -1;
        SetState(ClientState.InProcess);
    }

    // consultation finished, or the request was rejected
    public void Complete()
    {
        SetState(ClientState.Completed);
    }

    void SetState(ClientState newState)
    {
        if (state == newState) return;
        state = newState;
        OnStateChanged?.Invoke(this);
    }
}
