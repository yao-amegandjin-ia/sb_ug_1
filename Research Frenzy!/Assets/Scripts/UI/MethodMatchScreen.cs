namespace UI
{
  // MethodMatchScreen - goes on the MatchUI object in the UI scene (its own UIDocument with MethodologyMatchScreen.uxml).
// opens when the player clicks a researcher while a client follows them.
// shows the client's request and the researcher's methods. MATCH pays $100 for the best method,
// $50 for a good one and $0 otherwise. NO MATCH closes it and the client keeps following you.

using System.Collections.Generic;
using Assets.Scripts.Requests;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Method match screen goes on the MatchUI object in UI scene
/// This opens when the player clicks a researcher WHILE a client follows
/// Shows the client's request and researcher's methods.
/// Selecting a method and match pays $100 for best method, $50 for good, $0 otherwise
/// No match closes the screen
/// </summary>

//TODO: Probably needs to be made more efficient
[RequireComponent(typeof(UIDocument))]
public class MethodMatchScreen : MonoBehaviour
{
    public HUD hud;   // both are in the UI scene, so this can be set in the Inspector

    [Header("Rewards")]
    public int bestReward = 100;
    public int goodReward = 50;

    [Tooltip("How many of the researcher's methods can be picked")]
    [Range(1, 4)] public int unlockedMethods = 2;

    VisualElement root;
    Label nameText;
    Label problemText;
    TextElement bannerText;
    Label descriptionText;
    Button matchButton;

    readonly List<Button> methodButtons = new List<Button>();
    readonly List<int> shownIds = new List<int>();   // the methodology id on each button

    Client client;           // the client being matched right now
    Researcher researcher;   // the researcher who was clicked
    int selected = -1;       // index into methodButtons, -1 = nothing picked yet
    bool open;

    void Start()
    {
        if (hud == null) hud = FindAnyObjectByType<HUD>();

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
        bannerText = root.Q<TextElement>("method-banner-text");
        descriptionText = root.Q<Label>("method-description");
        matchButton = root.Q<Button>("match-btn");

        root.Q<Button>("close-btn").clicked += Close;
        matchButton.clicked += MatchClicked;
        root.Q<Button>("no-match-btn").clicked += Close;

        // the four buttons inside the ResearcherMethodologyComponent template
        methodButtons.AddRange(root.Q("method-tray-slot").Query<Button>().ToList());
        for (int i = 0; i < methodButtons.Count; i++)
        {
            int index = i;   // copy for the lambda, otherwise every button would use the last i
            methodButtons[i].clicked += () => Select(index);
        }

        Close();
    }

    void Update()
    {
        // client or researcher got removed while the screen was open
        if (open && (!client || !researcher))
            Close();
    }

    public void Open(Client client, Researcher researcher)
    {
        this.client = client;
        this.researcher = researcher;

        nameText.text = client.Data.Name;
        problemText.text = client.Data.RequestText;

        // put the researcher's methods on the buttons, hide the spare ones
        shownIds.Clear();
        IReadOnlyList<int> known = researcher.Data.KnownMethodologyIds;
        for (int i = 0; i < methodButtons.Count; i++)
        {
            Button button = methodButtons[i];
            if (i < known.Count)
            {
                shownIds.Add(known[i]);
                button.text = MethodLibrary.GetName(known[i]);
                button.style.display = DisplayStyle.Flex;
                button.SetEnabled(i < unlockedMethods);
            }
            else
            {
                shownIds.Add(-1);
                button.style.display = DisplayStyle.None;
            }
        }

        Select(-1);

        root.style.display = DisplayStyle.Flex;
        open = true;
    }

    public void Close()
    {
        root.style.display = DisplayStyle.None;
        open = false;
        client = null;
        researcher = null;
    }

    // picks a method button (or clears the pick with -1) and shows its description
    void Select(int index)
    {
        selected = index;
        for (int i = 0; i < methodButtons.Count; i++)
            methodButtons[i].EnableInClassList("selected", i == index);

        if (index < 0)
        {
            bannerText.text = "PICK A METHOD";
            descriptionText.text = researcher != null
                ? $"Choose one of {researcher.Data.Name}'s methods above."
                : "";
            matchButton.SetEnabled(false);
            return;
        }

        MethodEntry method = FindMethod(shownIds[index]);
        bannerText.text = method != null ? method.name.ToUpper() : "UNKNOWN METHOD";
        descriptionText.text = method != null ? method.description : "";
        matchButton.SetEnabled(true);
    }

    void MatchClicked()
    {
        if (selected < 0 || !client || !researcher) return;

        int methodId = shownIds[selected];
        MethodologiesEnum picked = (MethodologiesEnum)methodId;
        Request request = client.Data.Request;

        // HUD study results: 0 = best, 1 = partial (good), 2 = wrong
        int reward = 0;
        int result = 2;
        if (request != null && request.IsResearchable)
        {
            if (picked == request.BestMethodology) { reward = bestReward; result = 0; }
            else if (request.GoodMethodologies != null && request.GoodMethodologies.Contains(picked)) { reward = goodReward; result = 1; }
        }

        if (hud != null)
        {
            hud.AddMoney(reward);
            hud.AddStudyResult(result);
        }
        Debug.Log($"{client.Data.Name} matched with {researcher.Data.Name} using {picked}: +${reward}");

        // for now the client is done straight away and leaves (no research timer yet)
        client.Data.AssignTo(researcher.Data.Id, methodId);
        client.Data.Complete();
        client.Leave();

        Close();
    }

    // MethodLibrary only has GetName, so look the whole entry up for the description
    static MethodEntry FindMethod(int id)
    {
        foreach (MethodEntry m in MethodLibrary.GetAll())
            if (m.id == id) return m;
        return null;
    }
}

}