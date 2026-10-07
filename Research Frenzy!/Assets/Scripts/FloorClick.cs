// FloorClick - goes on the floor tilemap.
// clicking the floor tells the player where to walk.
// tilemap collider needs Geometry Type = Polygons or only the edges get clicks.

using UnityEngine;
using UnityEngine.EventSystems;

public class FloorClick : MonoBehaviour, IPointerClickHandler
{
    public PlayerMover player;

    public void OnPointerClick(PointerEventData eventData)
    {
        // where on the floor the click landed
        player.MoveTo(eventData.pointerCurrentRaycast.worldPosition);
    }
}
