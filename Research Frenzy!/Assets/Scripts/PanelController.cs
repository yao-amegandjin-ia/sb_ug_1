// PanelController - goes on the HUD canvas.
// opens/closes the assign and upgrades panels and fills in the client's info.
// the wait / tomorrow / reject buttons hook into the functions at the bottom.

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Assets.Scripts.Requests;
using System.Collections.Generic;

public class PanelController : MonoBehaviour
{
    public GameObject dim;
    public GameObject assignPanel;
    public GameObject upgradesPanel;

    [FormerlySerializedAs("companyText")] [Header("Assign panel text (optional)")]
    public TMP_Text nameText;
    public TMP_Text requestText;
    
    public RequestList reqList;
    

    // client whose request is open right now
    Client currentClient;

    void Update()
    {
        // temporary: press U to open upgrades until the day system exists
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            OpenUpgrades();

        // client ran out of patience while the panel was open
        if (currentClient && currentClient.state == ClientState.LeftDissatisfied)
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
        
        int requestId = currentClient.requestId;

        int index = reqList.FindIndex(x => x.RequestId == requestId);

        if (nameText) nameText.text = reqList.;
        if (requestText) requestText.text = "\"" + client.requestText + "\"";


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
