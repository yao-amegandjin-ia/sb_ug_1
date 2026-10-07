// ClickableClient - goes on the client object.
// clicking the client makes the player walk over, and the assign panel opens once they're close enough.
// needs a Collider2D on the object + Physics 2D Raycaster on the camera to work.

using UnityEngine;
using UnityEngine.EventSystems;

public class ClickableClient : MonoBehaviour, IPointerClickHandler
{
    public PanelController panels;
    public PlayerMover player;
    public float talkDistance = 1.2f;   // how close the player has to get before the panel opens

    Client client;

    void Awake()
    {
        client = GetComponent<Client>();

        // prefabs can't keep links to scene objects, so find them if they're empty
        if (panels == null) panels = FindAnyObjectByType<PanelController>();
        if (player == null) player = FindAnyObjectByType<PlayerMover>();
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
        if (this == null || !CanTalk()) return;

        if (client != null) panels.OpenAssignFor(client);
        else panels.OpenAssign();
    }

    // only clients that haven't been helped yet
    bool CanTalk()
    {
        if (client == null) return true;
        return client.state == ClientState.InProcess || client.state == ClientState.Waiting;
    }
}