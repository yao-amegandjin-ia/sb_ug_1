using System.Collections.Generic;
using Researchers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;



namespace UI
{
    /// <summary>
    /// ResearcherHireScreen
    /// This is what will appear when hiring a new researcher
    /// Currently, this will display from a button press. Later will show with daily progression at the end of day.
    /// </summary>
    //TODO: Probably needs to be made more efficient
    //TODO: May switch to panel renderer in the future?
    [RequireComponent(typeof(UIDocument))]
    public class ResearcherHireScreen : MonoBehaviour {
        
        //drag ResearcherHireScreen.uss in here. added in code so it works whatever the UXML's Style path says
        [SerializeField] private StyleSheet styleSheet;
 //drag ResearcherMethodologyComponent.uxml in here
        [SerializeField] private VisualTreeAsset methodologyComponent;
 
        private VisualElement root;
        private bool isOpen;
 
        private void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            
            if (styleSheet != null) root.styleSheets.Add(styleSheet);
            else Debug.LogWarning("ResearcherHireScreen: no Style Sheet set, the screen will be unstyled.");
 
            // cover the whole screen, dim the lab behind it and center the screen
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
 
            Button backButton = root.Q<Button>("back-to-upgrades-button");
            if (backButton != null) backButton.clicked += Close;
 
            Close();
        }
 
        // --- opening and closing (the Spawn Researcher button calls Toggle) ---
 
        public void Open()
        {
            isOpen = true;
            root.style.display = DisplayStyle.Flex;
            BuildCards();
        }
 
        public void Close()
        {
            isOpen = false;
            root.style.display = DisplayStyle.None;
        }
 
        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }
 
        //One card per researcher who hasn't been hired yet
        private void BuildCards()
        {
            ScrollView researchers = root.Q<ScrollView>("researchers");
            if (researchers == null) return;
            researchers.Clear();
 
            ResearcherGenerator generator = FindAnyObjectByType<ResearcherGenerator>();
            if (generator == null)
            {
                Debug.LogWarning("ResearcherHireScreen: no ResearcherGenerator found. Is it in the Research_Lab scene?");
                return;
            }
 
            foreach (ResearcherData r in generator.GetUnhired())
            {
                VisualElement stats = new VisualElement()
                {
                    name = "researcher-stats"
                };
 
                TextElement name = new TextElement()
                {
                    name = "researcher-name",
                    text = r.Name
                };
 
                TextElement quality = new TextElement()
                {
                    name = "quality",
                    text = "QUALITY: " + r.Quality + " / 3"
                };
 
                TextElement speed = new TextElement() {
                    name = "speed",
                    text = "SPEED: " + r.Speed + " / 3"
                };
 
                stats.Add(quality);
                stats.Add(speed);
 
                VisualElement researcherOverview = new VisualElement()
                {
                    name = "researcher-overview"
                };
 
                VisualElement nameAndStats = new VisualElement()
                {
                    name = "name-and-stats"
                };
 
                nameAndStats.Add(name);
                nameAndStats.Add(stats);
 
                researcherOverview.Add(nameAndStats);
 
                // the same four-button component the match screen uses, filled with this researcher's methods
                if (methodologyComponent != null)
                {
                    VisualElement researcherMethologies = methodologyComponent.Instantiate();
                    List<Button> methodologies = researcherMethologies.Query<Button>().ToList();
                    for (int i = 0; i < methodologies.Count; i++)
                    {
                        if (i < r.KnownMethodologyIds.Count)
                            methodologies[i].text = MethodLibrary.GetName(r.KnownMethodologyIds[i]);
                        else
                            methodologies[i].style.display = DisplayStyle.None;
                    }
                    researcherOverview.Add(researcherMethologies);
                }
 
                VisualElement hiringInfo = new VisualElement()
                {
                    name = "hiring-info"
                };
 
                ResearcherData toHire = r;   // copy for the lambda
                Button hireButton = new Button(() => HireClicked(toHire)) {
                    name = "hire-researcher",
                    text = "HIRE"
                };
 
                hiringInfo.Add(hireButton);
 
                VisualElement researcher = new VisualElement() {
                    name = "researcher"
                };
 
                researcher.Add(researcherOverview);
                researcher.Add(hiringInfo);
                researchers.Add(researcher);
            }
        }
 
        private void HireClicked(ResearcherData data)
        {
            GameInitializer game = FindAnyObjectByType<GameInitializer>();
            if (!game)
            {
                Debug.LogWarning("ResearcherHireScreen: no GameInitializer found. Did you press Play from the Bootstrap scene?");
                return;
            }
 
            if (game.HireResearcher(data) != null)
                Close();
        }
    }
}
