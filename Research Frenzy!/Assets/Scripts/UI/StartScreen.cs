// StartScreen - goes on the GameUI object (the one with the UIDocument).
// page 1 is the research link (intro), page 2 is picking the starting researcher.
// when the player hits Start Day 1 the start screen hides and GameStarted fires.

using System;
using UnityEngine;
using UnityEngine.UIElements;
using Assets.Scripts.GameDataObjects;

public class StartScreen : MonoBehaviour
{
    // placeholder until the real starting researchers are decided
    public string[] starterNames = { "Steven", "Maya" };
    public MethodologiesEnum[] starterMethods = { MethodologiesEnum.Product, MethodologiesEnum.Qualitative };
    public int selectedStarter = 0;

    // the HUD (and later the client spawner / day system) listen to this
    public event Action<int> GameStarted;

    VisualElement screen;
    VisualElement introPage;
    VisualElement pickPage;
    VisualElement[] cards;

    void Start()
    {
        UIDocument doc = GetComponent<UIDocument>();
        if (doc == null) return;
        VisualElement root = doc.rootVisualElement;

        screen = root.Q("start-screen");
        introPage = root.Q("intro-page");
        pickPage = root.Q("pick-page");

        root.Q<Button>("leave-button").clicked += LeaveClicked;
        root.Q<Button>("continue-button").clicked += ContinueClicked;
        root.Q<Button>("start-button").clicked += StartClicked;

        // build the two researcher cards
        VisualElement cardRow = root.Q("starter-cards");
        cards = new VisualElement[starterNames.Length];
        for (int i = 0; i < starterNames.Length; i++)
        {
            int index = i; // copy so each card keeps its own number
            Button card = new Button(() => SelectStarter(index));
            card.AddToClassList("starter-card");

            Label name = new Label(starterNames[i]);
            name.AddToClassList("starter-name");
            Label level = new Label("Level 1");
            level.AddToClassList("starter-level");

            int methodId = (int)starterMethods[i];
            VisualElement methodRow = new VisualElement();
            methodRow.AddToClassList("starter-method");
            Label badge = new Label(methodId.ToString());
            badge.AddToClassList("method-badge");
            Label methodName = new Label(MethodLibrary.GetName(methodId));
            methodName.AddToClassList("starter-method-name");
            methodRow.Add(badge);
            methodRow.Add(methodName);

            Label tag = new Label("SELECTED");
            tag.AddToClassList("selected-tag");

            card.Add(name);
            card.Add(level);
            card.Add(methodRow);
            card.Add(tag);
            cardRow.Add(card);
            cards[i] = card;
        }

        SelectStarter(selectedStarter);
        screen.RemoveFromClassList("hidden");
        introPage.RemoveFromClassList("hidden");
        pickPage.AddToClassList("hidden");
    }

    void ContinueClicked()
    {
        introPage.AddToClassList("hidden");
        pickPage.RemoveFromClassList("hidden");
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

    void StartClicked()
    {
        Debug.Log("Starting day 1 with " + starterNames[selectedStarter]);
        screen.AddToClassList("hidden");
        GameStarted?.Invoke(selectedStarter);
    }

    public string GetStarterName()
    {
        return starterNames[selectedStarter];
    }

    public int GetStarterMethod()
    {
        return (int)starterMethods[selectedStarter];
    }
}
