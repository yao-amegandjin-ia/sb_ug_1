namespace UI
{
    // RequestScreen - goes on the RequestUI object in the UI scene (its own UIDocument with ResearcherRequestScreen.uxml).
// first screen when the player talks to a client: shows their name and problem.
// "we can help" opens the referral screen, "we can't help" sends the client away.

using Clients;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Request Screen goes on the RequestUI object in the UI Scene
/// First screen in player interaction with client, where client shows their name and problem
/// "We can help" opens referral screen
/// "We can't help" sends client away
/// </summary>

[RequireComponent(typeof(UIDocument))]
public class RequestScreen : MonoBehaviour
{
    public ReferralScreen referral;   //both are in the UI scene, so this can be set in the Inspector

    VisualElement root;
    TextElement nameText;
    TextElement problemText;

    Client current;   //Current client, whose request is open currently
    bool open;

    void Start()
    {
        if (referral == null) referral = FindAnyObjectByType<ReferralScreen>();

        root = GetComponent<UIDocument>().rootVisualElement;

        //Cover the whole screen, Dim the lab behind it and center the card
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;
        root.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);

        // these are TextElements in the UXML, not Labels. the problem text has no name, so find it inside its parent
        nameText = root.Q<TextElement>("client-name");
        problemText = root.Q("problem-statement").Q<TextElement>();

        root.Q<Button>("close-btn").clicked += Close;
        root.Q<Button>("accept-btn").clicked += AcceptClicked;
        root.Q<Button>("deny-btn").clicked += DenyClicked;

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

    // "We can help" - on to the referral screen for the same client
    void AcceptClicked()
    {
        Client client = current;
        Close();
        if (client && referral != null) referral.Open(client);
    }

    // "We can't help" - the client leaves (for now their body is just destroyed)
    void DenyClicked()
    {
        if (current)
        {
            current.Data.Complete();   // "request was rejected"
            current.Leave();
        }
        Close();
    }
}
}