// ClickableClient - goes on the client object.
// clicking the client makes the player walk over, and the assign panel opens once they're close enough.
// needs a Collider2D on the object + Physics 2D Raycaster on the camera to work.

using Clients;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Game object for client. This is the component to be placed on the client prefab.
/// Data and game logic are in client data, which this holds.
/// Bind() connects client data to a client object
/// </summary>
public class Client : MonoBehaviour, IPointerClickHandler
{
    public PanelController panels;
    public PlayerMover player;
    public float talkDistance = 1.2f;   // how close the player has to get before the panel opens

    // Shown in the Inspector so you can watch patience and state at runtime. It points at the same
    // object the generator's callers and the panels use, so changes made in code show up here.
    [SerializeField] ClientData clientData;

    public ClientData Data => clientData;

    void Awake()
    {
        // prefabs can't keep links to scene objects, so find them if they're empty
        if (panels == null) panels = FindAnyObjectByType<PanelController>();
        if (player == null) player = FindAnyObjectByType<PlayerMover>();
    }

    /// <summary>Makes this object represent the given client. Call right after instantiating.</summary>
    public void Bind(ClientData clientData)
    {
        this.clientData = clientData;
        gameObject.name = $"Client {clientData.Id} - {clientData.Name}";
    }

    /// <summary>
    /// Patience timer update. The patience timer is in ClientData, so this
    /// updates the pateince timer there.
    /// </summary>
    void Update()
    {
        // the patience timer lives in ClientData, this just drives it
        if (clientData != null) clientData.Tick(Time.deltaTime);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (panels == null || !CanTalk()) return;

        // no player in the scene, just open it
        if (player == null)
        {
            OpenPanel();
            return;
        }

        player.WalkUpTo(transform, talkDistance, OpenPanel);
    }

    void OpenPanel()
    {
        // client might have left while the player was walking over
        if (!this || !CanTalk()) return;

        if (clientData != null) panels.OpenAssignFor(clientData);
        else panels.OpenAssign();
    }

    // only clients that haven't been helped yet
    bool CanTalk()
    {
        if (clientData == null) return true;
        return clientData.State == ClientState.InProcess || clientData.State == ClientState.Waiting;
    }
}