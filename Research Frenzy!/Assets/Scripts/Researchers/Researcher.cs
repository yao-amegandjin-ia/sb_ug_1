using UnityEngine;
using UnityEngine.EventSystems;
using Researchers;
using UI;

/// <summary>
///
/// Prefab component for researcher.
/// This holds no data or logic, ResearcherData.cs holds all of that.
/// This component displays what is in ResearcherData.cs.
/// The prefab is the same for all researchers.
/// Bind() applies the ResearcherData to this object, making each Researcher different.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]

//TODO: Is there a cheaper way to find the objects? Including player?
public class Researcher : MonoBehaviour, IPointerClickHandler
{
    // Shown in the Inspector so you can watch stats at runtime. It points at the same
    // object the hiring UI and save system use, so changes made in code show up here.
    
    //This is shown in the Inspector so we can see stats during runtime.
    //Points to the same object
    [SerializeField] private ResearcherData data;
    
    // Optional: sprite layers for the look. Arrays are indexed by ResearcherData.Appearance
    // (x: head, y: torso, z: legs). Leave empty if you set the look some other way.
    [Header("Appearance (optional)")]
    [SerializeField] private SpriteRenderer headRenderer;
    [SerializeField] private SpriteRenderer torsoRenderer;
    [SerializeField] private SpriteRenderer legsRenderer;
    [SerializeField] private Sprite[] headSprites;
    [SerializeField] private Sprite[] torsoSprites;
    [SerializeField] private Sprite[] legSprites;
    
    public ResearcherData Data => data;
    
    [Header("Talking")]
    public MethodMatchScreen matchScreen;
    public PlayerMover player;
    public float talkDistance = 1.2f;   // how close the player has to get before the screen opens
    
    
    void Awake()
    {
        // prefabs can't keep links to scene objects, so find them if they're empty
        if (matchScreen == null) matchScreen = FindAnyObjectByType<MethodMatchScreen>();
        if (player == null) player = FindAnyObjectByType<PlayerMover>();
    }

    /// <summary>Makes this object represent the given researcher. Call right after instantiating.</summary>
    public void Bind(ResearcherData researcherData)
    {
        data = researcherData;
        gameObject.name = data.Name;
        ApplyAppearance();
    }
    
    //This sets the sprites to render for the respective parts.
    //Appearance is a Vector3Int, x = head, y = torso, z = leg
    private void ApplyAppearance()
    {
        Vector3Int look = data.Appearance;
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
    
    // only does something when you've brought a client over
    public void OnPointerClick(PointerEventData eventData)
    {
        if (matchScreen == null) matchScreen = FindAnyObjectByType<MethodMatchScreen>();
        Debug.Log($"Clicked researcher {gameObject.name}");
        
        if (matchScreen == null) { Debug.LogWarning("Researcher: no MethodMatchScreen found. Is MatchUI set up in the UI scene?"); return; }
        if (data == null) { Debug.LogWarning("Researcher: no data. Was it spawned through the ResearcherSpawner?"); return; }
        if (!data.IsAvailable) { Debug.Log($"{data.Name} isn't available (hired: {data.IsPurchased}, state: {data.CurrentState})."); return; }
 
        Client client = FindFollowingClient();
        if (client == null)
        {
            Debug.Log($"{data.Name}: bring a client over first.");
            return;
        }
 
        // no player in the scene, just open it
        if (player == null)
        {
            OpenMatch(client);
            return;
        }
 
        player.WalkUpTo(transform, talkDistance, () => OpenMatch(client));
    }
 
    void OpenMatch(Client client)
    {
        // the client might have left while the player was walking over
        if (!this || !client || !client.IsFollowing) return;
 
        matchScreen.Open(client, this);
    }
 
    // the client tagging along behind the player, or null
    static Client FindFollowingClient()
    {
        ClientSpawner spawner = FindAnyObjectByType<ClientSpawner>();
        if (spawner == null) return null;
 
        foreach (Client c in spawner.ActiveClients)
            if (c != null && c.IsFollowing) return c;
        return null;
    }


    
}