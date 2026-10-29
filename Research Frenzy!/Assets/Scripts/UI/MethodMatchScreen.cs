


using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// Method match screen goes on the MatchUI object in UI scene
    /// This opens when the player clicks a researcher WHILE a client follows
    /// Shows the client's request and researcher's methods.
    /// Selecting a method and match pays $100 for best method, $50 for good, $0 otherwise
    /// No match closes the screen
    /// </summary>
    public class MethodMatchScreen : ClientScreen
    {
        private TextElement bannerText;
        private TextElement descriptionText;
        private Button matchButton;
        private MethodologyTray tray;

        private Researcher researcher;   // the researcher who was clicked
        private int selected = -1;       // index of the picked tray button, -1 = nothing picked yet

        protected override void BindUI(VisualElement root)
        {
            base.BindUI(root);   // client name + problem text

            bannerText = Find<TextElement>("method-banner-text");
            descriptionText = Find<TextElement>("method-description");
            matchButton = Find<Button>("match-btn");
            if (matchButton != null) matchButton.clicked += MatchClicked;

            HookButton("close-btn", Close);
            HookButton("no-match-btn", Close);

            // the four buttons inside the ResearcherMethodologyComponent template
            tray = new MethodologyTray(Find<VisualElement>("method-tray-slot"), Select);
        }

        public void Open(Client client, Researcher researcher)
        {
            if (!client || !researcher) return;
            this.researcher = researcher;
            OpenFor(client);
        }

        protected override void OnShown()
        {
            base.OnShown();
            int unlocked = Firm ? Firm.UnlockedMethodCount(researcher.Data) : 0;
            tray.Show(researcher.Data.KnownMethodologyIds, unlocked);
            Select(-1);
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            researcher = null;
            selected = -1;
        }

        // the client leaving (base) or the researcher getting removed both close it
        protected override bool ShouldAutoClose()
        {
            return base.ShouldAutoClose() || !researcher;
        }

        // picks a method button (or clears the pick with -1) and shows its description
        private void Select(int index)
        {
            selected = index;
            tray.SetSelected(index);

            if (index < 0)
            {
                SetText(bannerText, "PICK A METHOD");
                SetText(descriptionText, researcher != null ? $"Choose one of {researcher.Data.Name}'s methods above." : "");
                matchButton?.SetEnabled(false);
                return;
            }

            MethodEntry method = MethodLibrary.Get(tray.GetId(index));
            SetText(bannerText, method != null ? method.name.ToUpper() : "UNKNOWN METHOD");
            SetText(descriptionText, method != null ? method.description : "");
            matchButton?.SetEnabled(true);
        }

        // grading, money and the client leaving are all in FirmController.CompleteStudy
        private void MatchClicked()
        {
            if (selected < 0) return;
            if (Firm) Firm.CompleteStudy(Current, researcher, tray.GetId(selected));
            Close();
        }

        private static void SetText(TextElement element, string text)
        {
            if (element != null) element.text = text;
        }
    }
}
