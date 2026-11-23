// DaySummary - shows up when the day ends (FirmController.DayEnded), or when something calls Show(day).
// Continue asks FirmController for the next day.
// top: how many of each result today. middle: every client from today, what the player answered
// (approach + methodology), what it paid, the best answer and why (the Rationale column of the requests
// spreadsheet). bottom: money and Continue.
// it keeps its own list of today's clients from FirmController.ClientResolved. that event carries the request,
// the methodology picked and the outcome, so nothing has to be looked up in the saved study results.
// the Continue button is now SAVE & CONTINUE, next to a new SAVE & QUIT. Both save first
// (FirmController.SaveGame). If saving fails the error shows above the buttons, and the continue button turns into
// CONTINUE WITHOUT SAVING so the player is never stuck.


using System;
using System.Collections.Generic;
using GameDataObjects;
using GameLogic;
using UI.HUDScreens;
using UnityEngine;
using UnityEngine.UIElements;

public class DaySummary : MonoBehaviour, IGameUIPart
{
    // fires when the player presses Continue, before the next day starts. the upgrades screen can hook in here
    public event Action<int> Closed;


    /// <summary>SAVE & QUIT was pressed and the save worked. UIInitializer goes back to the start screen.</summary>
    public event Action<int> QuitRequested;

    readonly List<ClientResult> today = new List<ClientResult>();
    readonly List<ResearcherLevelUp> levelUpsToday = new List<ResearcherLevelUp>();   // RESEARCHER XP (new)

    VisualElement panel;
    VisualElement tiles;
    ScrollView list;
    Label title;
    Label earnedText;
    Label totalText;
    HUD hud;
    FirmController firmRules;
    int shownDay = -1;    // the day on screen, -1 when it's closed


    Button continueButton;
    Button quitButton;
    Label saveStatus;
    bool saving;        // true while a save is running, so a double click doesn't save twice
    bool saveFailed;    // the last save failed: continue without saving is allowed

    void Awake()
    {
        hud = GetComponent<HUD>();
    }

    // called by GameUIPanel once the UI has loaded (and again if it reloads)
    public void Bind(VisualElement root)
    {
        panel = root.Q("day-summary");
        tiles = root.Q("summary-tiles");
        list = root.Q<ScrollView>("summary-list");
        title = root.Q<Label>("summary-title");
        earnedText = root.Q<Label>("summary-earned");
        totalText = root.Q<Label>("summary-total");
        continueButton = root.Q<Button>("summary-continue");
        if (continueButton != null) continueButton.clicked += ContinueClicked;
        else Debug.LogWarning("DaySummary: no Button named 'summary-continue' in the GameUI UXML.");
        
        quitButton = root.Q<Button>("summary-save-quit");
        if (quitButton != null) quitButton.clicked += SaveAndQuitClicked;
        else Debug.LogWarning("DaySummary: no Button named 'summary-save-quit' in the GameUI UXML.");
        saveStatus = root.Q<Label>("summary-save-status");
        ResetSaveButtons();

        if (shownDay >= 0) Show(shownDay);   // the UI reloaded while the summary was open
    }

    void Start()
    {
        // the lab scene may not be loaded yet. if it isn't, HUD.Initialize(firm) calls Initialize below later
        if (firmRules == null) Initialize(FindAnyObjectByType<FirmController>());
    }

    /// <summary>Starts listening to the FirmController. Called by HUD.Initialize(firm) once the lab scene is up.</summary>
    public void Initialize(FirmController firm)
    {
        if (firm == firmRules) return;
        Disconnect();
        firmRules = firm;
        if (firmRules == null) return;
        firmRules.ClientResolved += OnClientResolved;
        firmRules.ResearcherLeveledUp += OnResearcherLeveledUp;   // RESEARCHER XP (new)
    }

    void Disconnect()
    {
        if (firmRules == null) return;
        firmRules.ClientResolved -= OnClientResolved;
        firmRules.ResearcherLeveledUp -= OnResearcherLeveledUp;   // RESEARCHER XP (new)
        firmRules = null;
    }

    void OnDestroy()
    {
        Disconnect();
    }

    // every finished client (matched, turned away or lost) comes through here
    void OnClientResolved(ClientResult result)
    {
        if (result != null) today.Add(result);
    }
    
    void OnResearcherLeveledUp(ResearcherLevelUp levelUp)
    {
        if (levelUp != null) levelUpsToday.Add(levelUp);
    }

    // --- the screen ---

    public void Show(int day)
    {
        shownDay = day;
        if (panel == null) return;   // the UI hasn't loaded yet, Bind shows it once it does

        title.text = "DAY " + day + " COMPLETE";

        tiles.Clear();
        AddTile("BEST", Count(ClientOutcome.Best), "best");
        AddTile("GOOD", Count(ClientOutcome.Good), "good");
        AddTile("INCORRECT", Count(ClientOutcome.Wrong) + Count(ClientOutcome.WrongReject), "wrong");
        AddTile("RIGHT APPROACH", CountRightApproach(), "good");
        AddTile("CORRECT REJECTS", Count(ClientOutcome.CorrectReject), "best");
        AddTile("CLIENTS LOST", Count(ClientOutcome.Lost), "lost");

        list.Clear();
        
        foreach (ResearcherLevelUp levelUp in levelUpsToday)
            list.Add(BuildLevelUpCard(levelUp));

        if (today.Count == 0)
        {
            Label empty = new Label("No clients today.");
            empty.AddToClassList("summary-empty");
            list.Add(empty);
        }
        foreach (ClientResult entry in today)
            list.Add(BuildCard(entry));

        earnedText.text = "$" + hud.earnedToday.ToString("N0");
        totalText.text = "$" + hud.money.ToString("N0");

        GetComponent<CodexPanel>().Close();
        GetComponent<ManageFirmPanel>().Close();
        ResetSaveButtons();
        panel.RemoveFromClassList("hidden");
    }

    int Count(ClientOutcome outcome)
    {
        int n = 0;
        foreach (ClientResult entry in today)
            if (entry.Outcome == outcome) n++;
        return n;
    }

    // matches where step one (the approach) was right
    int CountRightApproach()
    {
        int n = 0;
        foreach (ClientResult entry in today)
            if (entry.IsMatch && entry.ApproachCorrect) n++;
        return n;
    }

    void AddTile(string label, int value, string colorClass)
    {
        VisualElement tile = new VisualElement();
        tile.AddToClassList("summary-tile");
        tile.AddToClassList(colorClass);
        Label v = new Label(value.ToString());
        v.AddToClassList("tile-value");
        Label l = new Label(label);
        l.AddToClassList("tile-label");
        tile.Add(v);
        tile.Add(l);
        tiles.Add(tile);
    }

    // one client: name + result tags + money, the request, what the player answered, the best answer, and why
    VisualElement BuildCard(ClientResult entry)
    {
        RequestData request = entry.Request;

        VisualElement card = new VisualElement();
        card.AddToClassList("study-card");

        VisualElement top = new VisualElement();
        top.AddToClassList("study-card-top");
        Label name = new Label(request != null && !string.IsNullOrEmpty(request.Name) ? request.Name : "Client");
        name.AddToClassList("study-client");
        top.Add(name);

        // right side of the top row: [APPROACH RIGHT/WRONG] [BEST/GOOD/...] +$50
        VisualElement tags = new VisualElement();
        tags.AddToClassList("study-tags");
        if (entry.IsMatch)
            tags.Add(Tag(entry.ApproachCorrect ? "RIGHT APPROACH" : "WRONG APPROACH", entry.ApproachCorrect ? "best" : "wrong"));
        tags.Add(Tag(TagText(entry.Outcome), TagClass(entry.Outcome)));
        Label reward = new Label("+$" + entry.Reward.ToString("N0"));
        reward.AddToClassList("study-reward");
        tags.Add(reward);
        if (entry.IsMatch)
        {
            Label xp = new Label("+" + entry.ExperienceGained + " XP" +
                                 (string.IsNullOrEmpty(entry.ResearcherName) ? "" : "  ·  " + entry.ResearcherName));
            xp.AddToClassList("study-xp");
            if (entry.ExperienceGained == 0) xp.AddToClassList("none");
            tags.Add(xp);
        }
        top.Add(tags);
        card.Add(top);

        if (request != null)
        {
            Label problem = new Label(request.ProblemText);
            problem.AddToClassList("study-problem");
            card.Add(problem);
        }

        card.Add(AnswerRow("YOUR ANSWER", YourAnswer(entry), entry.MethodologyId));

        // when both steps were right, the best answer is the same as theirs, so it's not repeated
        bool gotItRight = (entry.Outcome == ClientOutcome.Best && entry.ApproachCorrect) || entry.Outcome == ClientOutcome.CorrectReject;
        if (!gotItRight && request != null)
        {
            int bestId = request.IsResearchable ? request.BestMethodologyId : MethodLibrary.NoneId;
            card.Add(AnswerRow("BEST ANSWER", BestAnswer(request), bestId));
        }

        if (request != null && !string.IsNullOrEmpty(request.Rationale))
        {
            Label why = new Label("WHY: " + request.Rationale);
            why.AddToClassList("study-why");
            card.Add(why);
        }

        return card;
    }
    
    VisualElement BuildLevelUpCard(ResearcherLevelUp levelUp)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("study-card");
        card.AddToClassList("levelup-card");

        VisualElement top = new VisualElement();
        top.AddToClassList("study-card-top");
        Label name = new Label(levelUp.Researcher != null ? levelUp.Researcher.Name : "Researcher");
        name.AddToClassList("study-client");
        top.Add(name);
        VisualElement tags = new VisualElement();
        tags.AddToClassList("study-tags");
        tags.Add(Tag("LEVEL " + levelUp.NewLevel, "best"));
        top.Add(tags);
        card.Add(top);

        string text = levelUp.UnlockedMethodologyId > 0
            ? "Can now use " + MethodLibrary.GetName(levelUp.UnlockedMethodologyId)
            : "Reached level " + levelUp.NewLevel;
        card.Add(AnswerRow("LEVEL UP", text, levelUp.UnlockedMethodologyId));
        return card;
    }

    VisualElement AnswerRow(string label, string text, int methodId)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("answer-row");

        Label l = new Label(label);
        l.AddToClassList("answer-label");
        row.Add(l);

        if (methodId > 0)
        {
            Label badge = new Label(methodId.ToString());
            badge.AddToClassList("method-badge");
            badge.AddToClassList("small-badge");
            row.Add(badge);
        }

        Label t = new Label(text);
        t.AddToClassList("answer-text");
        row.Add(t);
        return row;
    }

    string YourAnswer(ClientResult entry)
    {
        switch (entry.Outcome)
        {
            case ClientOutcome.Lost: return "They left before you helped them";
            case ClientOutcome.CorrectReject:
            case ClientOutcome.WrongReject: return "We can't help";
            default:
                // e.g. "Qualitative: Field Research", the same shape as the best answer below
                string method = MethodLibrary.GetName(entry.MethodologyId);
                return entry.Approach != ResearchApproach.None ? entry.Approach + ": " + method : method;
        }
    }

    Label Tag(string text, string colorClass)
    {
        Label tag = new Label(text);
        tag.AddToClassList("result-tag");
        tag.AddToClassList(colorClass);
        return tag;
    }

    // e.g. "Qualitative: Field Research". the approach (step one of matching) comes first
    string BestAnswer(RequestData request)
    {
        if (!request.IsResearchable) return "We can't help, this can't be researched";

        string method = MethodLibrary.GetName(request.BestMethodologyId);
        return request.Approach != ResearchApproach.None ? request.Approach + ": " + method : method;
    }

    string TagText(ClientOutcome outcome)
    {
        switch (outcome)
        {
            case ClientOutcome.Best: return "BEST METHOD";
            case ClientOutcome.Good: return "GOOD METHOD";
            case ClientOutcome.Wrong: return "WRONG METHOD";
            case ClientOutcome.CorrectReject: return "CORRECT REJECT";
            case ClientOutcome.Lost: return "LEFT";
            default: return "INCORRECT";
        }
    }

    string TagClass(ClientOutcome outcome)
    {
        switch (outcome)
        {
            case ClientOutcome.Best:
            case ClientOutcome.CorrectReject: return "best";
            case ClientOutcome.Good: return "good";
            case ClientOutcome.Lost: return "lost";
            default: return "wrong";
        }
    }

    //  SAVE & CONTINUE. Saves, then does what Continue always did
    async void ContinueClicked()
    {
        if (shownDay < 0 || saving) return;

        // the save failed and the player chose to carry on anyway
        if (!saveFailed && !await Save()) return;

        int day = shownDay;
        shownDay = -1; // Consume this Continue action immediately.

        if (panel != null) panel.AddToClassList("hidden");
        today.Clear();
        levelUpsToday.Clear();
        Closed?.Invoke(day);

    }

    //SAVE & QUIT. Only quits if the save worked, so progress is never lost by quitting
    async void SaveAndQuitClicked()
    {
        if (shownDay < 0 || saving) return;
        if (!await Save()) return;

        int day = shownDay;
        shownDay = -1;
        if (panel != null) panel.AddToClassList("hidden");
        today.Clear();
        levelUpsToday.Clear();
        QuitRequested?.Invoke(day);
    }
    
    // "async" + "await": the function pauses at the await until the save is done, without freezing the game,
    // then carries on. The buttons are greyed out in between so they can't be pressed twice.
    async Awaitable<bool> Save()
    {
        if (firmRules == null) return true;   // no lab loaded (testing the UI on its own): nothing to save

        saving = true;
        SetButtonsEnabled(false);
        SetStatus("Saving...", false);

        bool ok = await firmRules.SaveGame();

        saving = false;
        SetButtonsEnabled(true);
        if (ok)
        {
            saveFailed = false;
            SetStatus("", false);
        }
        else
        {
            saveFailed = true;
            SetStatus("Couldn't save: " + firmRules.LastSaveError, true);
            if (continueButton != null) continueButton.text = "CONTINUE WITHOUT SAVING";
        }
        return ok;
    }

    //back to normal each time the summary opens
    void ResetSaveButtons()
    {
        saving = false;
        saveFailed = false;
        SetButtonsEnabled(true);
        SetStatus("", false);
        if (continueButton != null) continueButton.text = "SAVE & CONTINUE";
        if (quitButton != null) quitButton.text = "SAVE & QUIT";
    }

    void SetButtonsEnabled(bool enabled)
    {
        continueButton?.SetEnabled(enabled);
        quitButton?.SetEnabled(enabled);
    }

    void SetStatus(string text, bool isError)
    {
        if (saveStatus == null) return;
        saveStatus.text = text;
        saveStatus.EnableInClassList("hidden", string.IsNullOrEmpty(text));
        saveStatus.EnableInClassList("error", isError);
    }
}
