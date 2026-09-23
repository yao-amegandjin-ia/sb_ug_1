using UnityEngine;
using UnityEngine.InputSystem;

public class PanelController : MonoBehaviour
{
    public GameObject dim;
    public GameObject assignPanel;
    public GameObject upgradesPanel;

    void Update()
    {
        // temporary: press U to open upgrades until the day system exists
        if (Keyboard.current.uKey.wasPressedThisFrame)
            OpenUpgrades();
    }

    public void OpenAssign()
    {
        dim.SetActive(true);
        assignPanel.SetActive(true);
    }

    public void OpenUpgrades()
    {
        dim.SetActive(true);
        upgradesPanel.SetActive(true);
    }

    public void CloseAll()
    {
        dim.SetActive(false);
        assignPanel.SetActive(false);
        upgradesPanel.SetActive(false);
    }
}
