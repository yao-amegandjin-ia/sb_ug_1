// ResearcherHireScreen - goes on the HireUI object in the UI scene (PanelRenderer with ResearcherHireScreen.uxml).
// lists the researchers the FirmController says can be hired. HIRE asks it to put them in a cubicle.
// for now the Spawn Researcher button opens it. later it will show with daily progression at the end of the day.

using Researchers;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public class ResearcherHireScreen : ModalScreen
    {
        //drag ResearcherMethodologyComponent.uxml in here
        [SerializeField] private VisualTreeAsset methodologyComponent;

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

            foreach (ResearcherData r in Firm.GetHireCandidates())
                list.Add(BuildCard(r));
        }

        // one card per researcher. the element names match the ids in ResearcherHireScreen.uss
        private VisualElement BuildCard(ResearcherData r)
        {
            VisualElement stats = new VisualElement { name = "researcher-stats" };
            stats.Add(Text("quality", "QUALITY: " + r.Quality + " / 3"));
            stats.Add(Text("speed", "SPEED: " + r.Speed + " / 3"));

            VisualElement nameAndStats = new VisualElement { name = "name-and-stats" };
            nameAndStats.Add(Text("researcher-name", r.Name));
            nameAndStats.Add(stats);

            VisualElement overview = new VisualElement { name = "researcher-overview" };
            overview.Add(nameAndStats);

            // the same four-button component the match screen uses, filled with this researcher's methods
            if (methodologyComponent != null)
            {
                VisualElement methods = methodologyComponent.Instantiate();
                new MethodologyTray(methods).Show(r.KnownMethodologyIds);
                overview.Add(methods);
            }

            Button hireButton = new Button(() => HireClicked(r)) { name = "hire-researcher", text = "HIRE" };
            VisualElement hiringInfo = new VisualElement { name = "hiring-info" };
            hiringInfo.Add(hireButton);

            VisualElement card = new VisualElement { name = "researcher" };
            card.Add(overview);
            card.Add(hiringInfo);
            return card;
        }

        private static TextElement Text(string elementName, string text)
        {
            return new TextElement { name = elementName, text = text };
        }

        // hiring itself is FirmController.Hire. the screen only closes if it worked
        private void HireClicked(ResearcherData researcher)
        {
            if (Firm && Firm.Hire(researcher))
                Close();
        }
    }
}
