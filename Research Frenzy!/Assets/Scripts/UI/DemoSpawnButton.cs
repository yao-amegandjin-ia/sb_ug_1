namespace UI
{
    // DemoSpawnButton - goes on the GameUI object (the one with the HUD's UIDocument).
// temporary "Spawn Client" button for the demo.
// the lab is a different scene, so it finds the LabInitializer at runtime instead of through the Inspector.

    using UnityEngine;
    using UnityEngine.UIElements;
    
    /// <summary>
    /// This is SOLELY for demonstration purposes, and will not be used in game
    /// This goes on the GameUI object (which is the HUD)
    /// Creates a temporary "Spawn Client" button for demonstration purposes
    /// Also creates a temporary "Spawn Researcher" button for demo
    /// 
    /// </summary>

    [RequireComponent(typeof(UIDocument))]
    public class DemoSpawnButton : MonoBehaviour
    {
        void Start()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;

            Button button = new Button(SpawnClicked);
            button.text = "Spawn Client";
            button.style.position = Position.Absolute;
            button.style.top = 16;
            button.style.right = 16;
            root.Add(button);
            
            Button researcherButton = new Button(SpawnResearcherClicked);
            researcherButton.text = "Spawn Researcher";
            researcherButton.style.position = Position.Absolute;
            researcherButton.style.top = 52;   // just under Spawn Client
            researcherButton.style.right = 16;
            root.Add(researcherButton);
        }

        void SpawnClicked()
        {
            GameInitializer lab = FindAnyObjectByType<GameInitializer>();
            if (lab == null)
            {
                Debug.LogWarning("DemoSpawnButton: no LabInitializer found. Is the Research_Lab scene loaded?");
                return;
            }
            lab.SpawnNextClient();
        }
        
        void SpawnResearcherClicked()
        {
            ResearcherHireScreen hireScreen = FindAnyObjectByType<ResearcherHireScreen>();
            if (hireScreen == null)
            {
                Debug.LogWarning("DemoSpawnButton: no ResearcherHireScreen found. Is it under UIRoot in the UI scene?");
                return;
            }
            hireScreen.Toggle();
        }
    }
}