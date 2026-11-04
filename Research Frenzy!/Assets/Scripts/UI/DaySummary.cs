// DaySummary - shows up when the day timer runs out (HUD.DayEnded), or when something calls Show(day).
// top: how many of each result today. middle: every client from today, what the player answered,
// the best answer and why (the request's Rationale from requests.json). bottom: money and Continue.
// it keeps its own list of today's clients from FirmController's events. each event fires right after
// GameInitializer.RecordStudy, so the newest StudyResult is the one that just happened.

using System;
using System.Collections.Generic;
using Assets.Scripts.GameDataObjects;
using GameLogic;
using UnityEngine;
using UnityEngine.UIElements;

public class DaySummary : MonoBehaviour, IGameUIPart
{
    // what happened with one client today
    enum Kind { Best, Good, Wrong, CorrectReject, WrongReject, Lost }

    class Entry
    {
        public RequestData request;
        public int pickedMethod;   // 0 when the client was turned away or left
        public Kind kind;
    }

    // fires when the player presses Continue, before the next day starts. the upgrades screen can hook in here
    public event Action<int> Closed;

    readonly List<Entry> today = new List<Entry>();
    Dictionary<int, RequestData> requests;   // request ID -> request, filled the first time it's needed

    VisualElement panel;
    VisualElement tiles;
    ScrollView list;
    Label title;
    Label earnedText;
    Label totalText;
    HUD hud;
    GameInitializer game;
    FirmController firmRules;
    int seenResults;      // how many StudyResults there were the last time we looked
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

        game = FindAnyObjectByType<GameInitializer>();
        if (game != null) seenResults = game.StudyResults.Count;

        firmRules = FindAnyObjectByType<FirmController>();
        if (firmRules != null)
        {
            firmRules.StudyFinished += OnStudyFinished;
            firmRules.RequestRejected += OnRequestRejected;
            firmRules.ClientLost += OnClientLost;
        }
        else Debug.LogWarning("DaySummary: no FirmController found, the summary won't list today's clients.");
    }

    void OnDestroy()
    {
        if (hud != null) hud.DayEnded -= Show;
        if (firmRules != null)
        {
            firmRules.StudyFinished -= OnStudyFinished;
            firmRules.RequestRejected -= OnRequestRejected;
            firmRules.ClientLost -= OnClientLost;
        }
    }

    // --- collecting today's clients ---

    void OnStudyFinished(StudyOutcome outcome)
    {
        StudyResult result = TakeNewestResult();
        if (result == null) return;

        Kind kind = outcome == StudyOutcome.Best ? Kind.Best
                  : outcome == StudyOutcome.Good ? Kind.Good
                  : Kind.Wrong;
        Add(FindRequest(result.RequestId), result.MethodologyId, kind);
    }

    void OnRequestRejected(bool correct)
    {
        StudyResult result = TakeNewestResult();
        if (result == null) return;
        Add(FindRequest(result.RequestId), 0, correct ? Kind.CorrectReject : Kind.WrongReject);
    }

    void OnClientLost(Client client)
    {
        TakeNewestResult();   // the firm saved this one too (as methodology 0), so mark it as seen
        if (client == null) return;
        Add(client.Data.Request, 0, Kind.Lost);
    }

    // the StudyResult that was just saved, or null if nothing new was saved (e.g. the client had no request)
    StudyResult TakeNewestResult()
    {
        if (game == null) return null;
        int count = game.StudyResults.Count;
        if (count <= seenResults) return null;
        seenResults = count;
        return game.StudyResults[count - 1];
    }

    void Add(RequestData request, int pickedMethod, Kind kind)
    {
        today.Add(new Entry { request = request, pickedMethod = pickedMethod, kind = kind });
    }

    RequestData FindRequest(int id)
    {
        if (requests == null)
        {
            // the requests come from the DataManager, which the bootstrap hands to the UI scene
            UIInitializer ui = FindAnyObjectByType<UIInitializer>();
            var all = ui != null && ui.Systems != null ? ui.Systems.DataManager.RequestList?.Requests : null;
            if (all == null) return null;   // not loaded yet, try again next time

            requests = new Dictionary<int, RequestData>();
            foreach (RequestData r in all)
                if (r != null) requests[r.RequestId] = r;
        }

        requests.TryGetValue(id, out RequestData found);
        return found;
    }

    // --- the screen ---

    public void Show(int day)
    {
        shownDay = day;
        if (panel == null) return;   // the UI hasn't loaded yet, Bind shows it once it does

        title.text = "DAY " + day + " COMPLETE";

        tiles.Clear();
        AddTile("BEST", Count(Kind.Best), "best");
        AddTile("GOOD", Count(Kind.Good), "good");
        AddTile("INCORRECT", Count(Kind.Wrong) + Count(Kind.WrongReject), "wrong");
        AddTile("CORRECT REJECTS", Count(Kind.CorrectReject), "best");
        AddTile("CLIENTS LOST", Count(Kind.Lost), "lost");

        list.Clear();
        if (today.Count == 0)
        {
            Label empty = new Label("No clients today.");
            empty.AddToClassList("summary-empty");
            list.Add(empty);
        }
        foreach (Entry entry in today)
            list.Add(BuildCard(entry));

        earnedText.text = "$" + hud.earnedToday.ToString("N0");
        totalText.text = "$" + hud.money.ToString("N0");

        GetComponent<CodexPanel>().Close();
        GetComponent<ManageFirmPanel>().Close();
        panel.RemoveFromClassList("hidden");
    }

    int Count(Kind kind)
    {
        int n = 0;
        foreach (Entry entry in today)
            if (entry.kind == kind) n++;
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
    VisualElement BuildCard(Entry entry)
    {
        RequestData request = entry.request;

        VisualElement card = new VisualElement();
        card.AddToClassList("study-card");

        VisualElement top = new VisualElement();
        top.AddToClassList("study-card-top");
        Label name = new Label(request != null && !string.IsNullOrEmpty(request.Name) ? request.Name : "Client");
        name.AddToClassList("study-client");
        Label tag = new Label(TagText(entry.kind));
        tag.AddToClassList("result-tag");
        tag.AddToClassList(TagClass(entry.kind));
        top.Add(name);
        top.Add(tag);
        card.Add(top);

        if (request != null)
        {
            Label problem = new Label(request.ProblemText);
            problem.AddToClassList("study-problem");
            card.Add(problem);
        }

        card.Add(AnswerRow("YOUR ANSWER", YourAnswer(entry), entry.pickedMethod));

        // when they got it right, the best answer is the same as theirs, so it's not repeated
        bool gotItRight = entry.kind == Kind.Best || entry.kind == Kind.CorrectReject;
        if (!gotItRight && request != null)
        {
            int bestId = request.IsResearchable ? (int)request.BestMethodology : 0;
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

    string YourAnswer(Entry entry)
    {
        switch (entry.kind)
        {
            case Kind.Lost: return "They left before you helped them";
            case Kind.CorrectReject:
            case Kind.WrongReject: return "We can't help";
            default: return MethodLibrary.GetName(entry.pickedMethod);
        }
    }

    string BestAnswer(RequestData request)
    {
        return request.IsResearchable
            ? MethodLibrary.GetName((int)request.BestMethodology)
            : "We can't help, this can't be researched";
    }

    string TagText(Kind kind)
    {
        switch (kind)
        {
            case Kind.Best: return "BEST";
            case Kind.Good: return "GOOD";
            case Kind.CorrectReject: return "CORRECT REJECT";
            case Kind.Lost: return "LEFT";
            default: return "INCORRECT";
        }
    }

    string TagClass(Kind kind)
    {
        switch (kind)
        {
            case Kind.Best:
            case Kind.CorrectReject: return "best";
            case Kind.Good: return "good";
            case Kind.Lost: return "lost";
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
