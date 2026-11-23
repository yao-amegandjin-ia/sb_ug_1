// Client - one customer that walks into the lab.
// keeps their request info, patience timer and current state.
// the panel buttons call the public functions below to change what happens to them.

using System;
using GameDataObjects;
using UnityEngine;

namespace Clients
{
    /// <summary>
    /// These are the possible states a client may be in
    /// InProcess: just walked in, waiting at the door (the entrance) for the player to click them
    /// Waiting : sitting in the waiting area. The only state where patience goes down 
    /// Assisted: in a consultation with a researcher
    /// Returning: told to come back tomorrow
    /// Completed: done (consultation finished or request rejected)
    /// LeftDissatisfied: patience ran out
    /// ResearchDone: the researcher finished, waiting for the player to collect the result
    /// </summary>
    public enum ClientState
    {
        InProcess,        // just walked in, waiting at the door for the player to click them
        Waiting,          // sitting in the waiting area
        Assisted,         // in a consultation with a researcher
        Returning,        // told to come back tomorrow
        Completed,        // done (consultation finished or request rejected)
        LeftDissatisfied,  // patience ran out
        Following, // following the player
        ResearchDone
    }

    /// <summary>
    /// The central data and logic of a client. Plain data, no GameObject, so a client can exist
    /// (be generated, be returning tomorrow, be saved) without anyone standing in the lab.
    /// The Client component is the in-world body that displays one of these and calls Tick().
    /// </summary>
    [Serializable]
    public class ClientData
    {
        [Header("Info")]
        [SerializeField] private int id = 0;
        [SerializeField] private int requestId;
        [SerializeField] private RequestData request;
        [Header("Skin")]
        [SerializeField] private Vector3Int appearance = Vector3Int.zero;
        [Header("Patience")]
        [SerializeField] private float maxPatience = 30f;
        [SerializeField] private float currentPatience = 30f;
        [Header("Entrance")]
        [SerializeField] private float entranceWait = 0f;
        [SerializeField] private float entranceTimeLeft = 0f;
        //true while a screen is open, so entrance timer does not run when player is interacting
        [NonSerialized] private bool beingGreeted;
        [Header("Assignment")]
        [SerializeField] private int assignedResearcherId = -1;
        [SerializeField] private int assignedMethodologyId = -1;
        [Header("Research session")]
        [SerializeField] private float researchDuration = 0f;
        [SerializeField] private float researchTimeLeft = 0f;
        [Header("State")]
        [SerializeField] private ClientState state = ClientState.InProcess;

        public int Id => id;
        public int RequestId => requestId;
        public RequestData Request => request;
        public Vector3Int Appearance => appearance;
        public float MaxPatience => maxPatience;
        public float Patience => currentPatience;
        /// <summary>Seconds left before they give up waiting at the entrance and go to the waiting room.</summary>
        public float EntranceTimeLeft => entranceTimeLeft;
        /// <summary>True while they stand at the entrance (walked in, not helped, not sent anywhere yet).</summary>
        public bool IsAtEntrance => state == ClientState.InProcess;
        public int AssignedResearcherId => assignedResearcherId;
        public int AssignedMethodologyId => assignedMethodologyId;
        public ClientState State => state;

        // RESEARCH TIMER (new)
        /// <summary>Seconds left in the research session (0 when there's no session or it's over).</summary>
        public float ResearchTimeLeft => researchTimeLeft;
        /// <summary>Length of the research session, after the researcher's speed was applied.</summary>
        public float ResearchDuration => researchDuration;
        /// <summary>0 when the session just started, 1 when it's done. For a progress bar.</summary>
        public float ResearchProgress => researchDuration <= 0f ? 1f : 1f - researchTimeLeft / researchDuration;
        /// <summary>True while the client is with a researcher. They can't be clicked or helped by anyone else.</summary>
        public bool IsInResearch => state == ClientState.Assisted;
        /// <summary>True when the session is over and the player can collect the result.</summary>
        public bool IsReadyToCollect => state == ClientState.ResearchDone;
        public string RequestText => request != null ? request.ProblemText : "";

        // The client's name comes from the request.
        public string Name => request != null ? request.Name : "";

        //Events
        // the game manager can listen to these
        public event Action<ClientData> OnStateChanged;
        public event Action<ClientData> OnLeftDissatisfied;
        public event Action<ClientData> OnResearchFinished;
        public event Action<ClientData> OnEntranceWaitOver;

        // Replaces the old Update(). The Client component calls this every frame with Time.deltaTime.
        public void Tick(float deltaTime)
        {
            
            //client waits at entrance first, so entrance timer goes first
            if (state == ClientState.InProcess)
            {
                if (entranceTimeLeft > 0f && !beingGreeted)
                {
                    entranceTimeLeft -= deltaTime;
                    if (entranceTimeLeft <= 0f)
                    {
                        entranceTimeLeft = 0f;
                        OnEntranceWaitOver?.Invoke(this);
                    }
                }
            }
            
            //patience only drops when in the waiting room
            else if (state == ClientState.Waiting)
            {
                currentPatience -= deltaTime;

                if (currentPatience <= 0f)
                {
                    RunOutOfPatience();
                }
            }

            //  the research session counts down only while the client is with the researcher.
            // Patience doesn't drop during this time (the check above only runs for InProcess / Waiting).
            else if (state == ClientState.Assisted && researchTimeLeft > 0f)
            {
                researchTimeLeft -= deltaTime;
                if (researchTimeLeft <= 0f)
                    FinishResearch();
            }
        }

        // called when the client is created (by the ClientGenerator). Also stores the Request itself.
        public void Init(int id, RequestData request, float patience, Vector3Int appearance)
        {
            Init(id, request?.RequestId ?? 0, appearance, patience);
            this.request = request;
        }

        // Same as above, when you only have the request's ID (Request stays null). Remember if max patience changes
        //the default value here needs to change as well. 
        public void Init(int id, int requestId, Vector3Int appearance, float patience = 30f)
        {
            this.id = id;
            this.requestId = requestId;
            this.request = null;
            maxPatience = patience;
            currentPatience = patience;
            assignedResearcherId = -1;
            assignedMethodologyId = -1;
            researchDuration = 0f; 
            researchTimeLeft = 0f;
            entranceWait = 0f;  
            entranceTimeLeft = 0f;
            beingGreeted = false;
            this.appearance = appearance;

            // Always starts at the door, even if the object was reused.
            state = ClientState.InProcess;
        }
        
        /// <summary>
        /// The client gives up and leaves dissatisfied. Called when the patience timer hits 0,
        /// and by FirmController when they're asked to wait but there's no free seat.
        /// </summary>
        public void RunOutOfPatience()
        {
            if (state == ClientState.LeftDissatisfied) return;
            currentPatience = 0f;
            SetState(ClientState.LeftDissatisfied);
            OnLeftDissatisfied?.Invoke(this);
        }
        
        
        /// <summary>Starts the entrance timer. FirmController calls this when the client walks in.</summary>
        public void StartEntranceWait(float seconds)
        {
            entranceWait = Mathf.Max(0f, seconds);
            entranceTimeLeft = entranceWait;
        }

        /// <summary>
        /// True while the player has one of this client's screens open (ClientScreen calls it on open and close).
        /// The entrance timer is paused while it's true, so a client doesn't walk off mid-conversation.
        /// </summary>
        public void SetBeingGreeted(bool greeted)
        {
            beingGreeted = greeted;
        }

        // for the patience bar UI (0 to 1)
        public float GetPatiencePercent()
        {
            if (maxPatience <= 0f) return 0f;
            return currentPatience / maxPatience;
        }
        
        // "Ask them to wait". also works on a client who was following, if the player changes their mind
        public void SendToWaitingRoom()
        {
            if (state == ClientState.InProcess || state == ClientState.Waiting || state == ClientState.Following)
                SetState(ClientState.Waiting);
        }
        
        // "We can help with that" - follows the player for now
        public void StartFollowing()
        {
            if (state == ClientState.InProcess || state == ClientState.Waiting)
                SetState(ClientState.Following);
        }

        // "Assign" - researcher + methodology picked on the request screen
        public void AssignTo(int researcherId, int methodologyId)
        {
            assignedResearcherId = researcherId;
            assignedMethodologyId = methodologyId;
            SetState(ClientState.Assisted);
        }
        
        /// <summary>
        /// Starts the research session: same as AssignTo, plus a timer of the given length (already shortened
        /// by the researcher's speed). FirmController.StartResearch calls this when MATCH is pressed.
        /// </summary>
        public void StartResearch(int researcherId, int methodologyId, float duration)
        {
            researchDuration = Mathf.Max(0f, duration);
            researchTimeLeft = researchDuration;
            AssignTo(researcherId, methodologyId);   // state -> Assisted, so Tick starts counting down
            if (researchTimeLeft <= 0f) FinishResearch();   // a 0 second session finishes straight away
        }
        
        /// <summary>
        /// Ends the session: the timer ran out, or FirmController ends it early (the day ended).
        /// The client now waits for the player to collect the result.
        /// </summary>
        public void FinishResearch()
        {
            if (state != ClientState.Assisted) return;
            researchTimeLeft = 0f;
            SetState(ClientState.ResearchDone);
            OnResearchFinished?.Invoke(this);
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
            entranceWait = 0f; 
            entranceTimeLeft = 0f;
            beingGreeted = false;
            assignedResearcherId = -1;
            assignedMethodologyId = -1;
            researchDuration = 0f;
            researchTimeLeft = 0f;
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

        /// <summary>
        /// Independent copy with no event subscribers (the Request is shared, it's read-only data).
        /// </summary>
        public ClientData Clone()
        {
            ClientData copy = (ClientData)MemberwiseClone();
            copy.OnStateChanged = null;
            copy.OnLeftDissatisfied = null;
            copy.OnResearchFinished = null;
            copy.OnEntranceWaitOver = null;
            return copy;
        }
    }
}
