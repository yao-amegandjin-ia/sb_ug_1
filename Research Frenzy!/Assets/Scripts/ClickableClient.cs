using UnityEngine;
using UnityEngine.EventSystems;

public class ClickableClient : MonoBehaviour, IPointerClickHandler
{
    public PanelController panels;

    public void OnPointerClick(PointerEventData eventData)
    {
        panels.OpenAssign();
    }
}
