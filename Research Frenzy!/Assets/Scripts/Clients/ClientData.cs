// Client - one customer that walks into the lab.
// keeps their request info, patience timer and current state.
// the panel buttons call the public functions below to change what happens to them.

using System;
using Assets.Scripts.GameDataObjects;
using UnityEngine;

namespace Clients
{
    /// <summary>
    /// These are the possible states a client may be in
    /// InProcess: just walked in, waiting at the door for the player to click them
    /// Waiting : sitting in the waiting area
    /// Assisted: in a consultation with a researcher
    /// Returning: told to come back tomorrow
    /// Completed: done (consultation finished or request rejected)
    /// LeftDissatisfied: patience ran out
    /// </summary>
    public enum ClientState
    {
        InProcess,        // just walked in, waiting at the door for the player to click them
        Waiting,          // sitting in the waiting area
        Assisted,         // in a consultation with a researcher
        Returning,        // told to come back tomorrow
        Completed,        // done (consultation finished or request rejected)
        LeftDissatisfied,  // patience ran out
        Following // following the player
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
        [Header("Assignment")]
        //TODO: May need to add assignment to ResearcherData object? Not sure for now so leaving as is
        [SerializeField] private int assignedResearcherId = -1;
        [SerializeField] private int assignedMethodologyId = -1;
        [Header("State")]
        [SerializeField] private ClientState state = ClientState.InProcess;

        public int Id => id;
        public int RequestId => requestId;
        public RequestData Request => request;
        public Vector3Int Appearance => appearance;
        public float MaxPatience => maxPatience;
        public float Patience => currentPatience;
        public int AssignedResearcherId => assignedResearcherId;
        public int AssignedMethodologyId => assignedMethodologyId;
        public ClientState State => state;
        public string RequestText => request != null ? request.ProblemText : "";

        // The client's name comes from the request.
        public string Name => request != null ? request.Name : "";

        //Events
        // the game manager can listen to these
        public event Action<ClientData> OnStateChanged;
        public event Action<ClientData> OnLeftDissatisfied;

        // Replaces the old Update(). The Client component calls this every frame with Time.deltaTime.
        public void Tick(float deltaTime)
        {
            // patience only drops while the client hasn't been helped yet
            // (opening the request panel does NOT pause it, per the GDD)
            if (state == ClientState.InProcess || state == ClientState.Waiting)
            {
                currentPatience -= deltaTime;

                if (currentPatience <= 0f)
                {
                    currentPatience = 0f;
                    SetState(ClientState.LeftDissatisfied);
                    OnLeftDissatisfied?.Invoke(this);
                }
            }
        }

        // called when the client is created (by the ClientGenerator). Also stores the Request itself.
        public void Init(int id, RequestData request, float patience, Vector3Int appearance)
        {
            Init(id, request?.RequestId ?? 0, patience, appearance);
            this.request = request;
        }

        // Same as above, when you only have the request's ID (Request stays null).
        public void Init(int id, int requestId, float patience, Vector3Int appearance)
        {
            this.id = id;
            this.requestId = requestId;
            this.request = null;
            maxPatience = patience;
            currentPatience = patience;
            assignedResearcherId = -1;
            assignedMethodologyId = -1;
            this.appearance = appearance;

            // Always starts at the door, even if the object was reused.
            state = ClientState.InProcess;
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

        /// <summary>
        /// Independent copy with no event subscribers (the Request is shared, it's read-only data).
        /// </summary>
        public ClientData Clone()
        {
            ClientData copy = (ClientData)MemberwiseClone();
            copy.OnStateChanged = null;
            copy.OnLeftDissatisfied = null;
            return copy;
        }
    }
}