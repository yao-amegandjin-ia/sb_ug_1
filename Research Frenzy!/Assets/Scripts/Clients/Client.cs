// ClickableClient - goes on the client object.
// clicking the client makes the player walk over, and the assign panel opens once they're close enough.
// needs a Collider2D on the object + Physics 2D Raycaster on the camera to work.

using System.Net.Http;
using Clients;
using UI;
using UI.ClientScreens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using GameLogic;

/// <summary>
/// Game object for client. This is the component to be placed on the client prefab.
/// Data and game logic are in client data, which this holds.
/// Bind() connects client data to a client object
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Client : MonoBehaviour, IPointerClickHandler
{
    public RequestScreen requestScreen;
    public ReferralScreen referralScreen;
    public PlayerMover player;
    public float talkDistance = 1.2f;   // how close the player has to get before the panel opens
    
    [Header("Following")]
    public float followSpeed = 4f;
    public float followDistance = 1f;   // stays this far behind the player

    // Shown in the Inspector so you can watch patience and state at runtime. It points at the same
    // object the generator's callers and the panels use, so changes made in code show up here.
    [SerializeField] ClientData clientData;

    public ClientData Data => clientData;
    
    // Optional: sprite layers for the look. Arrays are indexed by ClientData.Appearance
    // (x: head, y: torso, z: legs). Leave empty if you set the look some other way.
    [Header("Appearance (optional)")]
    [SerializeField] private SpriteRenderer headRenderer;
    [SerializeField] private SpriteRenderer torsoRenderer;
    [SerializeField] private SpriteRenderer legsRenderer;
    [SerializeField] private Sprite[] headSprites;
    [SerializeField] private Sprite[] torsoSprites;
    [SerializeField] private Sprite[] legSprites;
    
    [Header("Research session (optional)")]
    [Tooltip("Shown while the client is with a researcher (e.g. a \"...\" speech bubble). Make it a child of the client prefab.")]
    [SerializeField] private GameObject researchingIcon;
    [Tooltip("Shown when the research is done and the player can collect the result (e.g. a \"!\" or \"$\" bubble).")]
    [SerializeField] private GameObject readyIcon;
    [Tooltip("A child object whose X scale goes from 0 to 1 as the research progresses (e.g. a thin sprite with its pivot on the left).")]
    [SerializeField] private Transform researchBar;
    [Tooltip("Optional. The parent of the research bar (frame + fill). Shown while researching. If empty, only Research Bar is shown/hidden.")]
    [SerializeField] private GameObject researchBarRoot;


    [Header("Patience timer (optional)")]
    [Tooltip("The parent of the patience bar (clock + frame + fill). Shown while patience is going down (in the waiting room).")]
    [SerializeField] private GameObject patienceBarRoot;
    [Tooltip("The fill of the patience bar. Its X scale goes from 1 (full patience) down to 0. Pivot on the left.")]
    [SerializeField] private Transform patienceBar;
    [Tooltip("Colour of the patience fill from empty (left) to full (right). Needs a SpriteRenderer on Patience Bar.")]
    [SerializeField] private Gradient patienceColors = DefaultPatienceColors();
    private SpriteRenderer patienceBarRenderer;   // found once, on the Patience Bar object
    
    Rigidbody2D rb;
    FirmController firm; 
    Transform followTarget;
    Vector3? walkTarget; 
    ClientSpawner spawner; 
    
    // true after "We can help with that", until the client leaves. researchers check this when clicked
    public bool IsFollowing => followTarget != null;

    void Awake()
    {
        // prefabs can't keep links to scene objects, so find them if they're empty
        if (requestScreen == null) requestScreen = FindAnyObjectByType<RequestScreen>();
        if (referralScreen == null) referralScreen = FindAnyObjectByType<ReferralScreen>();
        if (player == null) player = FindAnyObjectByType<PlayerMover>();
        
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic; //Kinematic makes sure that the object is only moved by code
    }

    /// <summary>Makes this object represent the given client. Call right after instantiating.</summary>
    public void Bind(ClientData clientData)
    {
        if (this.clientData != null)   // re-bound to a new client
        {
            this.clientData.OnLeftDissatisfied -= OnLeftDissatisfied;
            this.clientData.OnResearchFinished -= OnResearchFinished;   
            this.clientData.OnEntranceWaitOver -= OnEntranceWaitOver;  
            this.clientData.OnStateChanged -= OnStateChanged;  
        }
        this.clientData = clientData;
        gameObject.name = $"Client {clientData.Id} - {clientData.Name}";

        // patience ran out (timer, or no seat in the waiting area). the FirmController decides what that means
        clientData.OnLeftDissatisfied += OnLeftDissatisfied;

        // the research timer ran out. the FirmController frees the researcher
        clientData.OnResearchFinished += OnResearchFinished;

        //  nobody greeted them at the entrance in time. the FirmController sends them to wait
        clientData.OnEntranceWaitOver += OnEntranceWaitOver;
        //  walk to a seat when they're sent to the waiting room, free it when they get up
        clientData.OnStateChanged += OnStateChanged;

        UpdateResearchVisuals();
    }
    
    void OnDestroy()
    {
        if (clientData != null)
        {
            clientData.OnLeftDissatisfied -= OnLeftDissatisfied;
            clientData.OnResearchFinished -= OnResearchFinished;  
            clientData.OnEntranceWaitOver -= OnEntranceWaitOver;  
            clientData.OnStateChanged -= OnStateChanged;  
        }
    }


    ClientSpawner Spawner
    {
        get
        {
            if (spawner == null) spawner = FindAnyObjectByType<ClientSpawner>();
            return spawner;
        }
    }

    //  same idea as OnResearchFinished, the FirmController decides what happens
    void OnEntranceWaitOver(ClientData data)
    {
        if (Firm) Firm.HandleEntranceWaitOver(this);
    }

    // sent to the waiting room -> walk to a free seat. Anything else -> the seat is free again
    void OnStateChanged(ClientData data)
    {
        if (Spawner == null) return;
        if (data.State == ClientState.Waiting)
        {
            walkTarget = Spawner.TakeSeat(this);   // null = no seats set up in the scene, so they stay where they are
        }
        else
        {
            Spawner.FreeSeat(this);
            walkTarget = null;
        }
    }

    //  finds the game rules once and remembers them (FindAnyObjectByType searches the whole scene)
    FirmController Firm
    {
        get
        {
            if (firm == null) firm = FindAnyObjectByType<FirmController>();
            return firm;
        }
    }

    // same idea as OnLeftDissatisfied. The data only knows its timer hit 0,
    // the FirmController decides what that means (the researcher is free again, the log is written)
    void OnResearchFinished(ClientData data)
    {
        if (Firm) Firm.HandleResearchFinished(this);
    }

    // the data only knows its state changed. the body passes it to the game rules, which score it and remove us
    void OnLeftDissatisfied(ClientData data)
    {
        FirmController firm = FindAnyObjectByType<FirmController>();
        if (firm) firm.HandleLeftDissatisfied(this);
        else Leave();   // no rules loaded (testing a scene on its own), so at least don't stay in the lab
    }
    
    //This sets the sprites to render for the respective parts.
    //Appearance is a Vector3Int, x = head, y = torso, z = leg
    private void ApplyAppearance()
    {
        Vector3Int look = clientData.Appearance;
        SetSprite(headRenderer, headSprites, look.x);
        SetSprite(torsoRenderer, torsoSprites, look.y);
        SetSprite(legsRenderer, legSprites, look.z);
    }
    
    // Does nothing if the renderer or sprite list isn't set up, so a bare prefab still works.
    private static void SetSprite(SpriteRenderer target, Sprite[] sprites, int index)
    {
        if (target == null || sprites == null || index < 0 || index >= sprites.Length) return;
        target.sprite = sprites[index];
    }

    /// <summary>
    /// Patience timer update. The patience timer is in ClientData, so this
    /// updates the pateince timer there.
    /// </summary>
    void Update()
    {
        // the patience timer lives in ClientData, this updates that timer
        clientData?.Tick(Time.deltaTime);
        UpdateResearchVisuals();   // RESEARCH TIMER (new)
    }

    // shows the icons / progress bar for the current state. Does nothing for empty fields.
    void UpdateResearchVisuals()
    {
        bool researching = clientData != null && clientData.IsInResearch;
        bool ready = clientData != null && clientData.IsReadyToCollect;

        // only call SetActive when it changes, so it isn't done every frame for nothing
        if (researchingIcon && researchingIcon.activeSelf != researching) researchingIcon.SetActive(researching);
        if (readyIcon && readyIcon.activeSelf != ready) readyIcon.SetActive(ready);

        // ICONS (changed, Nov 21): shows the whole bar (frame + fill) when Research Bar Root is set
        SetShown(researchBarRoot ? researchBarRoot : researchBar ? researchBar.gameObject : null, researching);
        if (researchBar && researching) SetWidth(researchBar, clientData.ResearchProgress);

        UpdatePatienceVisuals();   // ICONS (new, Nov 21)
    }

    // the patience bar. Only shown while patience actually goes down (ClientData.Tick):
    // waiting at the door (InProcess) or in the waiting room (Waiting)
    void UpdatePatienceVisuals()
    {
        bool losingPatience = clientData != null && clientData.State == ClientState.Waiting;
        SetShown(patienceBarRoot ? patienceBarRoot : patienceBar ? patienceBar.gameObject : null, losingPatience);
        if (!patienceBar || !losingPatience) return;

        float percent = Mathf.Clamp01(clientData.GetPatiencePercent());
        SetWidth(patienceBar, percent);

        if (patienceBarRenderer == null) patienceBarRenderer = patienceBar.GetComponent<SpriteRenderer>();
        if (patienceBarRenderer != null && patienceColors != null) patienceBarRenderer.color = patienceColors.Evaluate(percent);
    }
    
    
    static void SetShown(GameObject target, bool shown)
    {
        // only call SetActive when it changes, so it isn't done every frame for nothing
        if (target && target.activeSelf != shown) target.SetActive(shown);
    }

    static void SetWidth(Transform bar, float amount)
    {
        Vector3 scale = bar.localScale;
        scale.x = amount;
        bar.localScale = scale;
    }
    
    static Gradient DefaultPatienceColors()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.91f, 0.30f, 0.24f), 0f),     // red, empty
                new GradientColorKey(new Color(0.95f, 0.77f, 0.06f), 0.5f),   // yellow, half
                new GradientColorKey(new Color(0.18f, 0.80f, 0.44f), 1f)      // green, full
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
    /// <summary>
    /// This is what is updated every tick. 
    /// </summary>
    
    void FixedUpdate()
    {
        if (!followTarget && walkTarget.HasValue)
        {
            float speed = Spawner != null ? Spawner.WalkSpeed : followSpeed;
            Vector2 seat = walkTarget.Value;
            rb.MovePosition(Vector2.MoveTowards(rb.position, seat, speed * Time.fixedDeltaTime));
            if (Vector2.Distance(rb.position, seat) < 0.01f) walkTarget = null;   // arrived
            return;
        }

        if (!followTarget) return;
        // walk toward the player until we're close enough, then wait
        Vector2 toTarget = (Vector2)followTarget.position - rb.position;
        if (toTarget.magnitude <= followDistance) return;

        Vector2 next = Vector2.MoveTowards(rb.position, followTarget.position, followSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        // busy with a researcher, so clicking does nothing until the timer is done
        if (clientData != null && clientData.IsInResearch)
        {
            Debug.Log($"{clientData.Name} is still with the researcher ({clientData.ResearchTimeLeft:0}s left).");
            return;
        }

        //  the research is done. walk over and collect the result, then they leave
        if (clientData != null && clientData.IsReadyToCollect)
        {
            if (player == null) CollectResult();
            else player.WalkUpTo(transform, talkDistance, CollectResult);
            return;
        }

        if (requestScreen == null || !CanTalk()) return;

        // no player in the scene, just open it
        if (player == null)
        {
            OpenRequest();
            return;
        }

        player.WalkUpTo(transform, talkDistance, OpenRequest);
    }

    /// <summary>
    /// Open Request Screen if client is able to be talked to.
    /// A client who is already following goes straight back to the referral screen.
    /// </summary>
    void OpenRequest()
    {
        // client might have left while the player was walking over
        if (!this || !CanTalk()) return;

        if (IsFollowing && referralScreen) referralScreen.Open(this);
        else requestScreen.Open(this);
    }

    // the money and the client leaving are in FirmController.CollectResearch
    void CollectResult()
    {
        // the client might be gone (e.g. the day ended) while the player was walking over
        if (!this || clientData == null || !clientData.IsReadyToCollect) return;
        if (Firm) Firm.CollectResearch(this);
    }

    /// <summary>
    /// Starts to follow player, see StartFollowing()
    /// </summary>
    public void FollowPlayer()
    {
        if (clientData != null) clientData.StartFollowing();
        walkTarget = null; 
        if (player != null) followTarget = player.transform;
    }
    
    /// <summary>
    /// Stops following the player and stays where they are. Set the client's state before calling this.
    /// </summary>
    public void StopFollowing()
    {
        followTarget = null;
    }
    
    /// <summary>
    /// Removes the client game object from the scene. This does not affect client state, so set before calling this.
    /// </summary>
    public void Leave()
    {
        followTarget = null;

        // go through the spawner so its ActiveClients list stays right
        ClientSpawner spawner = FindAnyObjectByType<ClientSpawner>();
        if (spawner != null) spawner.Remove(this);
        else Destroy(gameObject);
    }

    // only clients that haven't been helped yet
    bool CanTalk()
    {
        if (clientData == null) return true;
        return clientData.State == ClientState.InProcess || clientData.State == ClientState.Waiting || clientData.State== ClientState.Following;
    }
}
