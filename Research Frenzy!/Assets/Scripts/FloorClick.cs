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
