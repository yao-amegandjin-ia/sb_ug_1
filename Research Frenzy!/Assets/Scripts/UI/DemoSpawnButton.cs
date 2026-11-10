namespace UI
{
    using GameLogic;
    // DemoSpawnButton - goes on the GameUI object (the one with the HUD's UIDocument).
    // temporary "Spawn Client" button for the demo.
    // the lab is a different scene, so it finds the FirmController at runtime instead of through the Inspector.

    using UnityEngine;
    using UnityEngine.UIElements;
    
    /// <summary>
    /// This is SOLELY for demonstration purposes, and will not be used in game
    /// This goes on the GameUI object (which is the HUD)
    /// Creates a temporary "Spawn Client" button for demonstration purposes
    /// Also creates a temporary "Spawn Researcher" button for demo
    /// 
    /// </summary>

    [RequireComponent(typeof(GameUIPanel))]
    public class DemoSpawnButton : MonoBehaviour, IGameUIPart
    {
        // called by GameUIPanel once the UI has loaded (and again if it reloads)
        public void Bind(VisualElement root)
        {

            Button button = new Button(SpawnClicked);
            button.text = "Spawn Client";
            button.style.position = Position.Absolute;
            button.style.top = 16;
            button.style.right = 16;
            root.Add(button);
            
            Button upgradesButton = new Button(UpgradesClicked);
            upgradesButton.text = "Upgrades";
            upgradesButton.style.position = Position.Absolute;
            upgradesButton.style.top = 52;   // just under Spawn Client
            upgradesButton.style.right = 16;
            root.Add(upgradesButton);
        }
        
        // sends in the next client of the day. FirmController (in the Research_Lab scene) owns the day's client list now,
        // so the button asks it. LabInitializer no longer exists, which is why the old code could only print a warning
        void SpawnClicked()
        {
            FirmController firm = FindAnyObjectByType<FirmController>();
            if (firm == null)
            {
                Debug.LogWarning("DemoSpawnButton: no FirmController found. Is the Research_Lab scene loaded?");
                return;
            }
            firm.SpawnNextClient();
        }
        
        void UpgradesClicked()
        {
            ShopScreen shop = FindAnyObjectByType<ShopScreen>();
            if (shop == null)
            {
                Debug.LogWarning("DemoSpawnButton: no ShopScreen found. Is the ShopUI object under UIRoot in the UI scene?");
                return;
            }
            shop.Toggle();
        }
    }
}