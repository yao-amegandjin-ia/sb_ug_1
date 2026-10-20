namespace UI
{
   // ReferralScreen - goes on the ReferralUI object in the UI scene (its own UIDocument with ResearcherReferralScreen.uxml).
// opens after "we can help" on the request screen.
// "we can help with that" makes the client follow the player, wait / tomorrow use the ClientData functions.



using Clients;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Referral Screen goes on the ReferralUI object in the UI scene
/// Opens up after "We can help" on the request Screen
/// Selecting "We can help with that" makes the client follow the player
/// Selecting "Wait" or "Return Tomorrow" changes ClientState
/// </summary>

[RequireComponent(typeof(UIDocument))]
public class ReferralScreen : MonoBehaviour
{
    VisualElement root;
    Label nameText;
    Label problemText;

    Client current;   // client whose request is open right now
    bool open;

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;

        // cover the whole screen, dim the lab behind it and center the card
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);

        nameText = root.Q<Label>("client-name");
        problemText = root.Q<Label>("problem-text");

        root.Q<Button>("close-btn").clicked += Close;

        root.Q<Button>("help-option").clicked += HelpClicked;
        root.Q<Button>("wait-option").clicked += WaitClicked;
        root.Q<Button>("tomorrow-option").clicked += TomorrowClicked;

        Close();
    }

    void Update()
    {
        // client ran out of patience (or got removed) while the screen was open
        if (open && (!current || current.Data.State == ClientState.LeftDissatisfied))
            Close();
    }

    public void Open(Client client)
    {
        current = client;
        nameText.text = client.Data.Name;
        problemText.text = client.Data.RequestText;

        root.style.display = DisplayStyle.Flex;
        open = true;
    }

    public void Close()
    {
        root.style.display = DisplayStyle.None;
        open = false;
        current = null;
    }

    // --- options ---

    void HelpClicked()
    {
        if (current) current.FollowPlayer();
        Close();
    }

    void WaitClicked()
    {
        if (current) current.Data.SendToWaitingRoom();
        Close();
    }

    void TomorrowClicked()
    {
        if (current)
        {
            current.Data.AskToReturnTomorrow();
            current.Leave();
        }
        Close();
    }
}
}