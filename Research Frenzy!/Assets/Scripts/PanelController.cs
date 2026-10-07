// PanelController - goes on the HUD canvas.
// opens/closes the assign and upgrades panels and fills in the client's info.
// the wait / tomorrow / reject buttons hook into the functions at the bottom.

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PanelController : MonoBehaviour
{
    public GameObject dim;
    public GameObject assignPanel;
    public GameObject upgradesPanel;

    [Header("Assign panel text (optional)")]
    public TMP_Text companyText;
    public TMP_Text requestText;
    public TMP_Text rewardText;

    // client whose request is open right now
    Client currentClient;

    void Update()
    {
        // temporary: press U to open upgrades until the day system exists
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            OpenUpgrades();

        // client ran out of patience while the panel was open
        if (currentClient != null && currentClient.state == ClientState.LeftDissatisfied)
            CloseAll();
    }

    public void OpenAssign()
    {
        dim.SetActive(true);
        assignPanel.SetActive(true);
    }

    // opens the assign panel with this client's info
    public void OpenAssignFor(Client client)
    {
        currentClient = client;

        if (companyText != null) companyText.text = client.company;
        if (requestText != null) requestText.text = "\"" + client.requestText + "\"";
        if (rewardText != null) rewardText.text = client.reward + " prestige";

        OpenAssign();
    }

    public void OpenUpgrades()
    {
        dim.SetActive(true);
        upgradesPanel.SetActive(true);
    }

    // --- buttons on the assign panel ---

    public void WaitClicked()
    {
        if (currentClient != null) currentClient.SendToWaitingRoom();
        CloseAll();
    }

    public void TomorrowClicked()
    {
        if (currentClient != null) currentClient.AskToReturnTomorrow();
        CloseAll();
    }

    public void RejectClicked()
    {
        if (currentClient != null) currentClient.Complete();
        CloseAll();
    }

    public void CloseAll()
    {
        dim.SetActive(false);
        assignPanel.SetActive(false);
        upgradesPanel.SetActive(false);
        currentClient = null;
    }
}
