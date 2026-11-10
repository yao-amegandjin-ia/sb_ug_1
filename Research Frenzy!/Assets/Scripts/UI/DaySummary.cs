// DaySummary - shows up when the day timer runs out (HUD.DayEnded), or when something calls Show(day).
// top: how many of each result today. middle: every client from today, what the player answered,
// the best answer and why (the Rationale column of requests.csv). bottom: money and Continue.
// it keeps its own list of today's clients from FirmController.ClientResolved. that event carries the request,
// the methodology picked and the outcome, so nothing has to be looked up in the saved study results.

using System;
using System.Collections.Generic;
using GameDataObjects;
using GameLogic;
using UnityEngine;
using UnityEngine.UIElements;

public class DaySummary : MonoBehaviour, IGameUIPart
{
    // fires when the player presses Continue, before the next day starts. the upgrades screen can hook in here
    public event Action<int> Closed;

    readonly List<ClientResult> today = new List<ClientResult>();

    VisualElement panel;
    VisualElement tiles;
    ScrollView list;
    Label title;
    Label earnedText;
    Label totalText;
    HUD hud;
    FirmController firmRules;
    int shownDay = -1;    // the day on screen, -1 when it's closed

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
        Button continueButton = root.Q<Button>("summary-continue");
        if (continueButton != null) continueButton.clicked += ContinueClicked;
        else Debug.LogWarning("DaySummary: no Button named 'summary-continue' in the GameUI UXML.");

        if (shownDay >= 0) Show(shownDay);   // the UI reloaded while the summary was open
    }

    void Start()
    {
        hud.DayEnded += Show;

        // the lab scene may not be loaded yet. if it isn't, HUD.Initialize(firm) calls Initialize below later
        if (firmRules == null) Initialize(FindAnyObjectByType<FirmController>());
    }

    /// <summary>Starts listening to the FirmController. Called by HUD.Initialize(firm) once the lab scene is up.</summary>
    public void Initialize(FirmController firm)
    {
        if (firm == firmRules) return;
        if (firmRules != null) firmRules.ClientResolved -= OnClientResolved;
        firmRules = firm;
        if (firmRules != null) firmRules.ClientResolved += OnClientResolved;
    }

    void OnDestroy()
    {
        if (hud != null) hud.DayEnded -= Show;
        if (firmRules != null) firmRules.ClientResolved -= OnClientResolved;
    }

    // every finished client (matched, turned away or lost) comes through here
    void OnClientResolved(ClientResult result)
    {
        if (result != null) today.Add(result);
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
        AddTile("CORRECT REJECTS", Count(ClientOutcome.CorrectReject), "best");
        AddTile("CLIENTS LOST", Count(ClientOutcome.Lost), "lost");

        list.Clear();
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
        panel.RemoveFromClassList("hidden");
    }

    int Count(ClientOutcome outcome)
    {
        int n = 0;
        foreach (ClientResult entry in today)
            if (entry.Outcome == outcome) n++;
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

    // one client: name + result, the request, what the player answered, the best answer, and why
    VisualElement BuildCard(ClientResult entry)
    {
        RequestData request = entry.Request;

        VisualElement card = new VisualElement();
        card.AddToClassList("study-card");

        VisualElement top = new VisualElement();
        top.AddToClassList("study-card-top");
        Label name = new Label(request != null && !string.IsNullOrEmpty(request.Name) ? request.Name : "Client");
        name.AddToClassList("study-client");
        Label tag = new Label(TagText(entry.Outcome));
        tag.AddToClassList("result-tag");
        tag.AddToClassList(TagClass(entry.Outcome));
        top.Add(name);
        top.Add(tag);
        card.Add(top);

        if (request != null)
        {
            Label problem = new Label(request.ProblemText);
            problem.AddToClassList("study-problem");
            card.Add(problem);
        }

        card.Add(AnswerRow("YOUR ANSWER", YourAnswer(entry), entry.MethodologyId));

        // when they got it right, the best answer is the same as theirs, so it's not repeated
        bool gotItRight = entry.Outcome == ClientOutcome.Best || entry.Outcome == ClientOutcome.CorrectReject;
        if (!gotItRight && request != null)
        {
            int bestId = request.IsResearchable ? request.BestMethodologyId : MethodLibrary.NoneId;
            card.Add(AnswerRow("BEST ANSWER", BestAnswer(request), bestId));
        }

        if (request != null && !string.IsNullOrEmpty(request.Rationale))
        {
            Label why = new Label(request.Rationale);
            why.AddToClassList("study-why");
            card.Add(why);
        }

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
            default: return MethodLibrary.GetName(entry.MethodologyId);
        }
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
            case ClientOutcome.Best: return "BEST";
            case ClientOutcome.Good: return "GOOD";
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

    void ContinueClicked()
    {
        if (panel != null) panel.AddToClassList("hidden");
        int day = shownDay;
        shownDay = -1;
        today.Clear();
        Closed?.Invoke(day);

        if (hud.IsLastDay())
        {
            // TODO: final survey + leaderboard go here
            Debug.Log("Last day done - final survey would go here");
            return;
        }

        // TODO: open the upgrades screen here once it's done, then start the next day from there
        hud.NextDay();
    }
}
