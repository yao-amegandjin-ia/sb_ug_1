// PanelController - goes on the HUD canvas.
// opens/closes the assign and upgrades panels and fills in the client's info.
// the wait / tomorrow / reject buttons hook into the functions at the bottom.

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Assets.Scripts.Requests;
using System.Collections.Generic;
using Clients;

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
    ClientData _currentClientData;

    void Update()
    {
        // temporary: press U to open upgrades until the day system exists
        if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
            OpenUpgrades();

        // client ran out of patience while the panel was open
        if (_currentClientData != null && _currentClientData.State == ClientState.LeftDissatisfied)
            CloseAll();
    }

    public void OpenAssign()
    {
        dim.SetActive(true);
        assignPanel.SetActive(true);
    }

    // opens the assign panel with this client's info
    public void OpenAssignFor(ClientData clientData)
    {
        _currentClientData = clientData;
        
        int requestId = _currentClientData.RequestId;

        Request req = reqList.Requests.Find(x => x.RequestId == requestId);

        if (nameText) nameText.text = req.Name;
        if (requestText) requestText.text = "\"" + req.ProblemText + "\"";


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
        if (_currentClientData != null) _currentClientData.SendToWaitingRoom();
        CloseAll();
    }

    public void TomorrowClicked()
    {
        if (_currentClientData != null) _currentClientData.AskToReturnTomorrow();
        CloseAll();
    }

    public void RejectClicked()
    {
        if (_currentClientData != null) _currentClientData.Complete();
        CloseAll();
    }

    public void CloseAll()
    {
        dim.SetActive(false);
        assignPanel.SetActive(false);
        upgradesPanel.SetActive(false);
        _currentClientData = null;
    }
}
