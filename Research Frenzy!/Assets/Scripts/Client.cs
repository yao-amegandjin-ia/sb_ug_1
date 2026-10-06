using System;
using UnityEngine;

public enum ClientState
{
    InProcess,        
    Waiting,          
    Assisted,         
    Returning,        
    Completed,        
    LeLeftDissatisfiedft  
}

public class Client : MonoBehaviour
{
    [Header("Info")]
    public string company;
    public int reward;

    [TextArea] public string requestText;

    [Header("Skin (5 options each)")]
    public int headIndex;
    public int bodyIndex;
    public int legIndex;


    [Header("Patience")]
    public float maxPatience = 30f;
    public float currentPatience;

    [Header("Assignment")]
    public int assignedResearcherId = -1;
    public int assignedMethodologyId = -1;

    [Header("State")]
    public ClientState state = ClientState.InProcess;

    public event Action<Client> OnStateChanged;
    public event Action<Client> OnLeftDissatisfied;

    const int SKIN_OPTIONS = 5;

    void Awake()
    {
        currentPatience = maxPatience;
    }
    void Update()
    {

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

    public void Init(string company, string requestText, int reward, float patience)
    {
        this.company = company;
        this.requestText = requestText;
        this.reward = reward;
        maxPatience = patience;
        currentPatience = patience;
        assignedResearcherId = -1;
        assignedMethodologyId = -1;

        RandomizeSkin();
        SetState(ClientState.InProcess);
    }

    public void RandomizeSkin()
    {
        headIndex = UnityEngine.Random.Range(0, SKIN_OPTIONS);
        bodyIndex = UnityEngine.Random.Range(0, SKIN_OPTIONS);
        legIndex = UnityEngine.Random.Range(0, SKIN_OPTIONS);
    }

    public float GetPatiencePercent()
    {
        if (maxPatience <= 0f) return 0f;
        return currentPatience / maxPatience;
    }

    public void SendToWaitingRoom()
    {
        if (state == ClientState.InProcess || state == ClientState.Waiting)
            SetState(ClientState.Waiting);
    }

    public void AssignTo(int researcherId, int methodologyId)
    {
        assignedResearcherId = researcherId;
        assignedMethodologyId = methodologyId;
        SetState(ClientState.Assisted);
    }

    public void AskToReturnTomorrow()
    {
        SetState(ClientState.Returning);
    }

    public void Reenter()
    {
        currentPatience = maxPatience;
        assignedResearcherId = -1;
        assignedMethodologyId = -1;
        SetState(ClientState.InProcess);
    }

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
