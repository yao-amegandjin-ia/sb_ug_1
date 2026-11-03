// HireTab - the shop's hire tab. goes on the ShopUI object next to the ShopScreen.
// this is what ResearcherHireScreen used to do: one ResearcherCard.uxml per researcher the FirmController says can be hired.
// HIRE asks the FirmController to put them in a cubicle. the screen stays open and the list rebuilds without them.

using Researchers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class HireTab : ShopTab
    {
        // drag ResearcherCard.uxml in here (UI/Researcher Hire Screen)
        [SerializeField] private VisualTreeAsset researcherCard;

        public override void Fill(VisualElement list)
        {
            if (!Firm) return;

            if (researcherCard == null)
            {
                Debug.LogWarning("HireTab: no Researcher Card set. Drag ResearcherCard.uxml into it.");
                return;
            }

            bool hasDesk = Firm.HasFreeCubicle;
            int shown = 0;
            foreach (ResearcherData r in Firm.GetHireCandidates())
            {
                list.Add(BuildCard(r, hasDesk));
                shown++;
            }

            if (shown == 0) AddEmptyMessage(list, "Nobody left to hire.");
        }

        // the layout is all in ResearcherCard.uxml / .uss. this only fills in the named elements
        private VisualElement BuildCard(ResearcherData r, bool hasDesk)
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
            if (hireButton != null)
            {
                if (hasDesk) hireButton.clicked += () => HireClicked(r);
                else hireButton.text = "NO DESK";
                hireButton.SetEnabled(hasDesk);   // every cubicle is taken
            }

            return card;
        }

        // "Laufey Kim" -> "LK"
        private static string Initials(string fullName)
        {
            string initials = "";
            foreach (string part in fullName.Split(' '))
                if (part.Length > 0) initials += char.ToUpper(part[0]);
            return initials;
        }

        // hiring itself is FirmController.Hire. the shop stays open and the list rebuilds without them
        private void HireClicked(ResearcherData researcher)
        {
            if (Firm && Firm.Hire(researcher))
                Refresh();
        }
    }
}
