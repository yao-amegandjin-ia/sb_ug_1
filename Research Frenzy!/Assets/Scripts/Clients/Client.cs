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
    
    Rigidbody2D rb;
    Transform followTarget;
    
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
        this.clientData = clientData;
        gameObject.name = $"Client {clientData.Id} - {clientData.Name}";
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
    }
    /// <summary>
    /// This is what is updated every tick. 
    /// </summary>
    
    void FixedUpdate()
    {
        if (!followTarget) return;
        // walk toward the player until we're close enough, then wait
        Vector2 toTarget = (Vector2)followTarget.position - rb.position;
        if (toTarget.magnitude <= followDistance) return;

        Vector2 next = Vector2.MoveTowards(rb.position, followTarget.position, followSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }


    public void OnPointerClick(PointerEventData eventData)
    {
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

    /// <summary>
    /// Starts to follow player, see StartFollowing()
    /// </summary>
    public void FollowPlayer()
    {
        if (clientData != null) clientData.StartFollowing();
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