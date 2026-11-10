using GameDataObjects;
using UnityEngine.UIElements;

namespace UI.ClientScreens
{
    /// <summary>
    /// Method match screen goes on the MatchUI object in UI scene.
    /// Opens when the player clicks a researcher WHILE a client follows.
    /// Matching is two steps:
    ///   1. pick an approach (Quantitative / Qualitative / Mixed), then NEXT
    ///   2. pick one of the researcher's methodologies, then MATCH (CHANGE goes back to step 1)
    /// Grading and money are in FirmController.CompleteStudy. NO MATCH or CLOSE just closes the screen.
    /// </summary>
    public class MethodMatchScreen : ClientScreen
    {
        // the three approach buttons, in the same order as Approaches below
        private static readonly string[] ApproachButtonNames = { "approach-quantitative", "approach-qualitative", "approach-mixed" };
        private static readonly ResearchApproach[] Approaches = { ResearchApproach.Quantitative, ResearchApproach.Qualitative, ResearchApproach.Mixed };

        private TextElement promptSpeaker;
        private TextElement promptQuestion;
        private TextElement bannerText;
        private TextElement descriptionText;
        private TextElement pickedApproachText;
        private VisualElement approachStep;
        private VisualElement methodStep;
        private Button nextButton;
        private Button matchButton;
        private readonly Button[] approachButtons = new Button[3];
        private MethodologyTray tray;

        private Researcher researcher;                           // the researcher who was clicked
        private ResearchApproach approach = ResearchApproach.None; // step 1 pick, None = nothing picked yet
        private int selected = -1;                               // step 2 pick: index of the tray button, -1 = nothing picked yet

        protected override void BindUI(VisualElement root)
        {
            base.BindUI(root);   // client name + problem text

            promptSpeaker = Find<TextElement>("prompt-speaker");
            promptQuestion = Find<TextElement>("prompt-question");
            bannerText = Find<TextElement>("method-banner-text");
            descriptionText = Find<TextElement>("method-description");
            pickedApproachText = Find<TextElement>("picked-approach");
            approachStep = Find<VisualElement>("approach-step");
            methodStep = Find<VisualElement>("method-step");

            nextButton = Find<Button>("next-btn");
            if (nextButton != null) nextButton.clicked += NextClicked;
            matchButton = Find<Button>("match-btn");
            if (matchButton != null) matchButton.clicked += MatchClicked;

            HookButton("close-btn", Close);
            HookButton("no-match-btn", Close);
            HookButton("back-btn", ShowApproachStep);

            for (int i = 0; i < ApproachButtonNames.Length; i++)
            {
                approachButtons[i] = Find<Button>(ApproachButtonNames[i]);
                ResearchApproach a = Approaches[i];   // copy for the lambda, same reason as in MethodologyTray
                if (approachButtons[i] != null) approachButtons[i].clicked += () => SelectApproach(a);
            }

            // the four buttons inside the ResearcherMethodologyComponent template
            tray = new MethodologyTray(Find<VisualElement>("method-tray-slot"), SelectMethod);
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

            // every client starts fresh on step 1
            approach = ResearchApproach.None;
            selected = -1;
            ShowApproachStep();
        }

        protected override void OnHidden()
        {
            base.OnHidden();
            researcher = null;
            approach = ResearchApproach.None;
            selected = -1;
        }

        // the client leaving (base) or the researcher getting removed both close it
        protected override bool ShouldAutoClose()
        {
            return base.ShouldAutoClose() || !researcher;
        }

        // ---------- step 1: approach ----------

        // shows step 1. also what CHANGE does. the approach already picked stays picked
        private void ShowApproachStep()
        {
            SetVisible(approachStep, true);
            SetVisible(methodStep, false);
            SetVisible(nextButton, true);
            SetVisible(matchButton, false);
            SetText(promptSpeaker, "YOUR LAB - STEP 1 OF 2");
            SetText(promptQuestion, "WHICH APPROACH FITS?");
            SelectApproach(approach);
        }

        // highlights the approach and shows what it means. None clears the pick
        private void SelectApproach(ResearchApproach picked)
        {
            approach = picked;
            for (int i = 0; i < approachButtons.Length; i++)
                approachButtons[i]?.EnableInClassList("selected", Approaches[i] == picked);

            SetText(bannerText, ApproachTitle(picked));
            SetText(descriptionText, ApproachDescription(picked));
            nextButton?.SetEnabled(picked != ResearchApproach.None);
        }

        private void NextClicked()
        {
            if (approach == ResearchApproach.None) return;
            ShowMethodStep();
        }

        // ---------- step 2: methodology ----------

        private void ShowMethodStep()
        {
            SetVisible(approachStep, false);
            SetVisible(methodStep, true);
            SetVisible(nextButton, false);
            SetVisible(matchButton, true);
            SetText(promptSpeaker, "YOUR LAB - STEP 2 OF 2");
            SetText(promptQuestion, "WHICH METHOD FITS?");
            SetText(pickedApproachText, approach.ToString());
            SelectMethod(selected);
        }

        // picks a method button (or clears the pick with -1) and shows its description
        private void SelectMethod(int index)
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
            if (approach == ResearchApproach.None || selected < 0) return;
            if (Firm) Firm.CompleteStudy(Current, researcher, approach, tray.GetId(selected));
            Close();
        }

        // ---------- text for the approaches ----------
        // only three approaches and they're part of the game's rules (an enum), so their text lives here
        // instead of in a spreadsheet. New methodologies still go in the methodologies spreadsheet.

        private static string ApproachTitle(ResearchApproach a)
        {
            switch (a)
            {
                case ResearchApproach.Quantitative: return "QUANTITATIVE RESEARCH: COUNT AND MEASURE";
                case ResearchApproach.Qualitative: return "QUALITATIVE RESEARCH: LEARN THE WHY";
                case ResearchApproach.Mixed: return "MIXED METHODS: NUMBERS AND REASONS";
                default: return "PICK AN APPROACH";
            }
        }

        private static string ApproachDescription(ResearchApproach a)
        {
            switch (a)
            {
                case ResearchApproach.Quantitative:
                    return "Want to know how many or how much? Quantitative research collects numbers from many people, " +
                           "like surveys, measurements and experiments, so results can be counted and compared.";
                case ResearchApproach.Qualitative:
                    return "Want to know why? Qualitative research explores people's reasons, feelings, and experiences " +
                           "through interviews and open-ended questions, in their own words.";
                case ResearchApproach.Mixed:
                    return "Need both? Mixed methods combine numbers with people's own explanations, " +
                           "to measure what is happening and understand why.";
                default:
                    return "Is this a question about numbers, about reasons, or both? Choose an approach above.";
            }
        }

        // ---------- helpers ----------

        private static void SetText(TextElement element, string text)
        {
            if (element != null) element.text = text;
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
