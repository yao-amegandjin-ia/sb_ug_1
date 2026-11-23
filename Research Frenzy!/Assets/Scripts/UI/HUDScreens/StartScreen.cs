// StartScreen - goes on the GameUI object (the one with the PanelRenderer and GameUIPanel).
// page 1 is the research link (intro), page 2 is picking the starting researcher.
// when the player hits Start Day 1 the start screen hides and GameStarted fires.
//  page 1 now has LEAVE, CONTINUE (only when there's a save: loads it, ContinueRequested fires)
// and NEW GAME (goes to page 2, like the old CONTINUE button did).


using System;
using System.Collections.Generic;
using GameDataObjects;
using Researchers;
using UI.HUDScreens;
using UnityEngine;
using UnityEngine.UIElements;

public class StartScreen : MonoBehaviour, IGameUIPart
{
    public int selectedStarter = 0;   // which card is picked, 0 = the first entry in Starting Researchers

    // the HUD (and later the client spawner / day system) listen to this
    /// <summary>START DAY 1 on the pick page: a NEW game with this starter (0 = first card).</summary>
    public event Action<int> GameStarted;

    /// <summary>CONTINUE on page 1: carry on the saved game. UIInitializer listens.</summary>
    public event Action ContinueRequested;

    VisualElement screen;
    VisualElement introPage;
    VisualElement pickPage;
    VisualElement[] cards = new VisualElement[0];
    VisualElement cardRow;               
    ResearcherGenerator generator;     
    bool started = false;   // so a UI reload in the middle of the game doesn't bring the start screen back

    // set by UIInitializer once the save has loaded (ShowSaveOptions). The UI can load before that,
    // so they're remembered and applied again whenever Bind runs
    bool hasSave;
    string saveInfoText = "";
    Button continueButton;
    Label continueInfo;
    Label newGameWarning;

    // called by GameUIPanel once the UI has loaded (and again if it reloads)
    public void Bind(VisualElement root)
    {
        screen = root.Q("start-screen");
        introPage = root.Q("intro-page");
        pickPage = root.Q("pick-page");

        HookButton(root, "leave-button", LeaveClicked);
        HookButton(root, "continue-button", ContinueSavedClicked);
        HookButton(root, "new-game-button", NewGameClicked);
        HookButton(root, "start-button", StartClicked);
        
        continueButton = root.Q<Button>("continue-button");
        continueInfo = root.Q<Label>("continue-info");
        newGameWarning = root.Q<Label>("new-game-warning");
        ApplySaveOptions();

        // build the researcher cards
        cardRow = root.Q("starter-cards");
        BuildCards();

        if (started)
        {
            screen.AddToClassList("hidden");
            return;
        }
        screen.RemoveFromClassList("hidden");
        introPage.RemoveFromClassList("hidden");
        pickPage.AddToClassList("hidden");
    }

    void HookButton(VisualElement root, string buttonName, Action onClick)
    {
        Button button = root.Q<Button>(buttonName);
        if (button != null) button.clicked += onClick;
        else Debug.LogWarning($"StartScreen: no Button named '{buttonName}' in the GameUI UXML.");
    }


    void NewGameClicked()
    {
        BuildCards();  
        introPage.AddToClassList("hidden");
        pickPage.RemoveFromClassList("hidden");
    }
    
    void ContinueSavedClicked()
    {
        if (!hasSave) return;
        Debug.Log("Continuing the saved game");
        started = true;
        screen.AddToClassList("hidden");
        ContinueRequested?.Invoke();
    }

    /// <summary>
    /// Shows or hides CONTINUE. UIInitializer calls this once the save has loaded.
    /// info is the small line under the buttons, e.g. "Saved after day 3 of 7".
    /// </summary>
    public void ShowSaveOptions(bool saveExists, string info)
    {
        hasSave = saveExists;
        saveInfoText = info ?? "";
        ApplySaveOptions();
    }

    void ApplySaveOptions()
    {
        if (continueButton != null) continueButton.EnableInClassList("hidden", !hasSave);
        if (continueInfo != null)
        {
            continueInfo.text = saveInfoText;
            continueInfo.EnableInClassList("hidden", !hasSave);
        }
        // "Starting a new game deletes your saved game" on the pick page, only when there is one
        if (newGameWarning != null) newGameWarning.EnableInClassList("hidden", !hasSave);
    }

    void LeaveClicked()
    {
        // TODO: go back to the Cyclone Commons hub once we have their API
        Debug.Log("Leave clicked - would go back to Cyclone Commons hub");
    }

    void SelectStarter(int index)
    {
        selectedStarter = index;
        for (int i = 0; i < cards.Length; i++)
        {
            if (i == index)
                cards[i].AddToClassList("selected");
            else
                cards[i].RemoveFromClassList("selected");
        }
    }
    
    IReadOnlyList<StarterResearcher> Starters()
    {
        if (generator == null) generator = FindAnyObjectByType<ResearcherGenerator>();
        return generator != null ? generator.StartingResearchers : Array.Empty<StarterResearcher>();
    }
    
    void BuildCards()
    {
        if (cardRow == null) return;
        cardRow.Clear();
        IReadOnlyList<StarterResearcher> starters = Starters();
        cards = new VisualElement[starters.Count];
        for (int i = 0; i < starters.Count; i++)
        {
            int index = i; // copy so each card keeps its own number
            Button card = new Button(() => SelectStarter(index));
            card.AddToClassList("starter-card");
            if (starters[i] != null) BuildCard(card, starters[i]);
            cardRow.Add(card);
            cards[i] = card;
        }

        SelectStarter(Mathf.Clamp(selectedStarter, 0, Mathf.Max(0, starters.Count - 1)));
    }
    
    void BuildCard(VisualElement card, StarterResearcher starter)
    {
        List<int> methodIds = starter.GetMethodologyIds();
        int startLevel = ResearcherLevels.LevelFor(generator != null ? generator.StarterExperience : 0);
        Label name = new Label(starter.name);
        name.AddToClassList("starter-name");
        Label level = new Label("Level " + startLevel);
        level.AddToClassList("starter-level");
        card.Add(name);
        card.Add(level);
        

        Label stats = new Label($"SPEED {starter.speed}/3  ·  QUALITY {starter.quality}/3");
        stats.AddToClassList("starter-stats");
        card.Add(stats);

        // level 1's method: the one they can use from day 1 (the same row as the old card)
        card.Add(MethodRow(methodIds.Count > 0 ? methodIds[0] : MethodLibrary.NoneId, null));

        // levels 2-4: greyed out with the level they unlock at. Only the ones typed in the Inspector;
        // if fewer than 4 are typed, the rest are picked at random when the game starts and aren't shown here
        for (int i = 1; i < methodIds.Count; i++)
            card.Add(MethodRow(methodIds[i], i < startLevel ? null : "LV " + (i + 1)));

        Label tag = new Label("SELECTED");
        tag.AddToClassList("selected-tag");
        card.Add(tag);
    }


    VisualElement MethodRow(int methodId, string lockedLabel)
    {
        VisualElement methodRow = new VisualElement();
        methodRow.AddToClassList("starter-method");

        if (lockedLabel != null)
        {
            methodRow.AddToClassList("locked");
            Label lv = new Label(lockedLabel);
            lv.AddToClassList("starter-unlock-level");
            methodRow.Add(lv);
        }

        Label badge = new Label(methodId.ToString());
        badge.AddToClassList("method-badge");
        Label methodName = new Label(MethodLibrary.GetName(methodId));
        methodName.AddToClassList("starter-method-name");
        methodRow.Add(badge);
        methodRow.Add(methodName);

        // greyed out in code too, so it reads as "not yet" even before the USS is added
        if (lockedLabel != null) methodRow.style.opacity = 0.45f;
        return methodRow;
    }

    void StartClicked()
    {
        Debug.Log("Starting day 1 with " + GetStarterName());
        started = true;
        screen.AddToClassList("hidden");
        GameStarted?.Invoke(selectedStarter);
    }
    
    public string GetStarterName()
    {
        StarterResearcher starter = StarterAt(selectedStarter);
        return starter != null ? starter.name : "Unknown";
    }
    
    StarterResearcher StarterAt(int index)
    {
        IReadOnlyList<StarterResearcher> starters = Starters();
        return index >= 0 && index < starters.Count ? starters[index] : null;
    }

    public int GetStarterMethod()
    {
        return StarterMethodId(selectedStarter);
    }


    int StarterMethodId(int index)
    {
        StarterResearcher starter = StarterAt(index);
        List<int> ids = starter != null ? starter.GetMethodologyIds() : null;
        return ids != null && ids.Count > 0 ? ids[0] : MethodLibrary.NoneId;
    }
}
