// ResearcherHireScreen - goes on the HireUI object in the UI scene (PanelRenderer with ResearcherHireScreen.uxml).
// lists the researchers the FirmController says can be hired. HIRE asks it to put them in a cubicle.
// for now the Spawn Researcher button opens it. later it will show with daily progression at the end of the day.

using Researchers;
using UI.ClientScreens;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class ResearcherHireScreen : ModalScreen
    {
        //drag ResearcherCard.uxml in here (UI/Researcher Hire Screen)
        [SerializeField] private VisualTreeAsset researcherCard;
 
        private ScrollView list;
 
        protected override void BindUI(VisualElement root)
        {
            list = Find<ScrollView>("researchers");
            HookButton("back-to-upgrades-button", Close);
        }
 
        public void Open() => Show();
 
        // the Spawn Researcher button calls this
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }
 
        // rebuilt every time it opens, so researchers hired since last time are gone
        protected override void OnShown()
        {
            if (list == null) return;
            list.Clear();
            if (!Firm) return;
 
            if (researcherCard == null)
            {
                Debug.LogWarning("ResearcherHireScreen: no Researcher Card set. Drag ResearcherCard.uxml into it.");
                return;
            }
 
            foreach (ResearcherData r in Firm.GetHireCandidates())
                list.Add(BuildCard(r));
        }
 
        // the layout is all in ResearcherCard.uxml / .uss. this only fills in the named elements
        private VisualElement BuildCard(ResearcherData r)
        {
            VisualElement card = researcherCard.Instantiate();
 
            SetText(card, "researcher-name", r.Name);
            SetText(card, "quality", "QUALITY " + r.Quality + "/3");
            SetText(card, "speed", "SPEED " + r.Speed + "/3");
 
            // no pictures yet, so the portrait shows initials. once there are sprites:
            //   card.Q("portrait").style.backgroundImage = new StyleBackground(sprite);
            //   card.Q("portrait-initials").style.display = DisplayStyle.None;
            SetText(card, "portrait-initials", Initials(r.Name));
 
            new MethodologyTray(card.Q("methods")).Show(r.KnownMethodologyIds);
 
            Button hireButton = card.Q<Button>("hire-researcher");
            if (hireButton != null) hireButton.clicked += () => HireClicked(r);
 
            return card;
        }
 
        private static void SetText(VisualElement card, string elementName, string text)
        {
            TextElement element = card.Q<TextElement>(elementName);
            if (element != null) element.text = text;
        }
 
        // "Laufey Kim" -> "LK"
        private static string Initials(string fullName)
        {
            string initials = "";
            foreach (string part in fullName.Split(' '))
                if (part.Length > 0) initials += char.ToUpper(part[0]);
            return initials;
        }
 
        // hiring itself is FirmController.Hire. the screen only closes if it worked
        private void HireClicked(ResearcherData researcher)
        {
            if (Firm && Firm.Hire(researcher))
                Close();
        }
    }
}
