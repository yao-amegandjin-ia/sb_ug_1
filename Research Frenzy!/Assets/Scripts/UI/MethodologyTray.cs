// MethodologyTray - wraps the four buttons in ResearcherMethodologyComponent.uxml.
// the match screen and the hire screen both show a researcher's methods with it.
// not a MonoBehaviour, just a helper: make one with the component's root element.

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UI
{
    public class MethodologyTray
    {
        private readonly List<Button> buttons;
        private readonly List<int> ids = new List<int>();   // the methodology id on each button, -1 = hidden

        // onClick gets the index of the button that was pressed (0 to 3). leave it out for a display-only tray
        public MethodologyTray(VisualElement tray, Action<int> onClick = null)
        {
            buttons = tray != null ? tray.Query<Button>().ToList() : new List<Button>();
            if (onClick == null) return;

            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i;   // copy for the lambda, otherwise every button would use the last i
                buttons[i].clicked += () => onClick(index);
            }
        }

        // puts the methods on the buttons and hides the spare ones. buttons past "unlocked" are greyed out
        public void Show(IReadOnlyList<int> methodIds, int unlocked = int.MaxValue)
        {
            ids.Clear();
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                if (methodIds != null && i < methodIds.Count)
                {
                    ids.Add(methodIds[i]);
                    button.text = MethodLibrary.GetName(methodIds[i]);
                    button.style.display = DisplayStyle.Flex;
                    button.SetEnabled(i < unlocked);
                }
                else
                {
                    ids.Add(-1);
                    button.style.display = DisplayStyle.None;
                }
            }
        }

        // methodology id on a button, or -1
        public int GetId(int index)
        {
            return index >= 0 && index < ids.Count ? ids[index] : -1;
        }

        // adds the "selected" class from ResearcherMethodologyComponent.uss to one button (-1 clears it)
        public void SetSelected(int index)
        {
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].EnableInClassList("selected", i == index);
        }
    }
}
