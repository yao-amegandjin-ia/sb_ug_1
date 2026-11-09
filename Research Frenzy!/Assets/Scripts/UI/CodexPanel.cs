// CodexPanel - left side panel that lists every research method.
// clicking a method opens its description under it. only one can be open at a time.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CodexPanel : MonoBehaviour, IGameUIPart
{
    VisualElement panel;
    List<Button> rows = new List<Button>();
    List<Label> texts = new List<Label>();
    int openIndex = -1;

    // called by GameUIPanel once the UI has loaded (and again if it reloads)
    public void Bind(VisualElement root)
    {
        panel = root.Q("codex");
        Button close = root.Q<Button>("codex-close");
        if (close != null) close.clicked += Close;

        ScrollView list = root.Q<ScrollView>("codex-list");
        list.Clear();
        rows.Clear();
        texts.Clear();
        openIndex = -1;

        List<MethodEntry> methods = MethodLibrary.GetAll();
        for (int i = 0; i < methods.Count; i++)
        {
            int index = i;
            Button row = new Button(() => ToggleMethod(index));
            row.AddToClassList("method-row");

            Label badge = new Label(methods[i].id.ToString());
            badge.AddToClassList("method-badge");
            Label name = new Label(methods[i].name);
            name.AddToClassList("method-name");
            row.Add(badge);
            row.Add(name);

            Label text = new Label(methods[i].description);
            text.AddToClassList("method-text");
            text.AddToClassList("hidden");

            list.Add(row);
            list.Add(text);
            rows.Add(row);
            texts.Add(text);
        }
    }

    void ToggleMethod(int index)
    {
        // close whatever is open first
        if (openIndex >= 0)
        {
            rows[openIndex].RemoveFromClassList("open");
            texts[openIndex].AddToClassList("hidden");
        }

        if (openIndex == index)
        {
            openIndex = -1; // clicked the open one again, so just close it
            return;
        }

        openIndex = index;
        rows[index].AddToClassList("open");
        texts[index].RemoveFromClassList("hidden");
    }

    public void Open()
    {
        if (panel != null) panel.RemoveFromClassList("hidden");
    }

    public void Close()
    {
        if (panel != null) panel.AddToClassList("hidden");
    }

    public bool IsOpen()
    {
        return panel != null && !panel.ClassListContains("hidden");
    }
}
