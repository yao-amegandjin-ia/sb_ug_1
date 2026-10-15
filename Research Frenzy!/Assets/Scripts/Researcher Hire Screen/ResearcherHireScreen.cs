using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using UnityEditor;


[Serializable]
public class Researcher
{
    public string name;
    public int fame;
    public int speed;
    public int hireCost;
    public string bio;
    public string specialtyBlurb;
    public List<string> startingMethods;
    public List<string> learnableMethods;
}

[Serializable]
public class ResearcherData
{
    public List<Researcher> researchers;
}

// Manages dynamic loading and unloading of UI assets using Unity's Addressable Asset System.
// This script loads both VisualTreeAsset and StyleSheet assets at runtime and applies them to a PanelRenderer component,
// providing efficient memory management and asynchronous loading capabilities.

public class ResearcherHireScreen : MonoBehaviour
{
    [SerializeField] private string uxmlAssetKey; // Addressable key for the UXML asset to load.
    [SerializeField] private AssetReference ussAsset; // AssetReference for the USS asset to load.
    private PanelRenderer panelRenderer;
    private AsyncOperationHandle<VisualTreeAsset> uxmlLoadHandle;
    private AsyncOperationHandle<StyleSheet> ussLoadHandle;
    private VisualTreeAsset loadedAsset;
    private StyleSheet loadedStyleSheet;

    private void OnEnable()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        if (panelRenderer != null)
        {
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }
    }
    
    private void OnDisable()
    {
        if (panelRenderer != null)
        {
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }
        UnloadUIDocument();
    }
    
    // Callback method invoked when the PanelRenderer reloads the UI.
    private void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
    {
        // Load assets when UI is ready to receive them.
        if (!uxmlLoadHandle.IsValid())
        {
            LoadUIDocument();
        }
        
        if (!ussLoadHandle.IsValid())
        {
            LoadStyleSheet();
        }
        
        // Apply stylesheet if already loaded; guard against adding the same sheet on every reload.
        if (loadedStyleSheet != null && !rootElement.styleSheets.Contains(loadedStyleSheet))
        {
            rootElement.styleSheets.Add(loadedStyleSheet);
        }

        TextAsset jsonFile = Resources.Load<TextAsset>("game_data");
        ResearcherData data = JsonUtility.FromJson<ResearcherData>(jsonFile.text);
        List<Researcher> allResearchers = data.researchers;

        foreach (Researcher r in allResearchers)
        {
            Debug.Log(r.name + " costs " + r.hireCost);

            VisualElement stats = new VisualElement() 
        {
            name = "researcher-stats"
        };

        TextElement name = new TextElement() 
        {
            name = "researcher-name",
            text = r.name
        };

        TextElement fame = new TextElement() 
        {
            name = "fame",
            text = "FAME: " + r.fame + " / 3"
        };

        TextElement speed = new TextElement() {
            name = "speed",
            text = "SPEED: " + r.speed + " / 3"
        };

        stats.Add(fame);
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

        VisualTreeAsset uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Researcher Methodology Component/ResearcherMethodologyComponent.uxml");
        VisualElement researcherMethologies = uiAsset.Instantiate();
        List<Button> methodologies = researcherMethologies.Query<Button>().ToList();
        foreach (Button b in methodologies) {
            Color c = RandomColor();
            b.style.borderTopColor = c;
            b.style.borderRightColor = c;
            b.style.borderBottomColor = c;
            b.style.borderLeftColor = c;
            b.style.backgroundColor = Lighten(c, 0.2f);
        }


        researcherOverview.Add(nameAndStats);
        researcherOverview.Add(researcherMethologies);

        VisualElement researcherBio = new VisualElement(){
            name = "researcher-bio"
        };

        TextElement bioText = new TextElement()
        {
            name = "bio-text",
            text = r.bio
        };
        
        TextElement specialityBlurb = new TextElement()
        {
            name = "speciality-blurb",
            text = r.specialtyBlurb
        };

        researcherBio.Add(bioText);
        researcherBio.Add(specialityBlurb);

        VisualElement hiringInfo = new VisualElement() 
        {
            name = "hiring-info"
        };

        TextElement costLabel = new TextElement() {
            text = "COST: "
        };

        TextElement cost = new TextElement() {
            name = "cost",
            text = r.hireCost + " PRESTIGE"
        };

        VisualElement costInfo = new VisualElement() {
            name = "cost-info"
        };
        costInfo.Add(costLabel);
        costInfo.Add(cost);

        Button hireButton = new Button() {
            name = "hire-researcher",
            text = "HIRE"
        };

        hiringInfo.Add(costInfo);
        hiringInfo.Add(hireButton);

        ScrollView researchers = rootElement.Q<ScrollView>();
        if (researchers == null) return;

        VisualElement researcher = new VisualElement() {
            name = "researcher"
        };

        researcher.Add(researcherOverview);
        researcher.Add(researcherBio);
        researcher.Add(hiringInfo);
        researchers.Add(researcher);
        }

        
    }
    
    private void LoadUIDocument()
    {
        Debug.Log($"Loading {uxmlAssetKey}");
        uxmlLoadHandle = Addressables.LoadAssetAsync<VisualTreeAsset>(uxmlAssetKey);
        uxmlLoadHandle.Completed += UxmlHandle_Completed;
    }
    
    private void LoadStyleSheet()
    {
        if (ussAsset != null)
        {
            Debug.Log($"Loading {ussAsset.RuntimeKey}");
            ussLoadHandle = Addressables.LoadAssetAsync<StyleSheet>(ussAsset);
            ussLoadHandle.Completed += UssHandle_Completed;
        }
        else
        {
            Debug.LogWarning("USS AssetReference is null, skipping style sheet loading.");
        }
    }
    
    private void UxmlHandle_Completed(AsyncOperationHandle<VisualTreeAsset> obj)
    {
        if (!isActiveAndEnabled || panelRenderer == null)
            return;
            
        if (obj.Status == AsyncOperationStatus.Succeeded)
        {
            loadedAsset = obj.Result;
            panelRenderer.visualTreeAsset = loadedAsset;
        }
        else
        {
            Debug.LogError($"AssetKey {uxmlAssetKey} failed to load.");
        }
    }
    
    private void UssHandle_Completed(AsyncOperationHandle<StyleSheet> obj)
    {
        if (!isActiveAndEnabled || panelRenderer == null)
            return;
            
        if (obj.Status == AsyncOperationStatus.Succeeded)
        {
            loadedStyleSheet = obj.Result;
        }
        else
        {
            Debug.LogError($"AssetReference {(ussAsset != null ? ussAsset.RuntimeKey : "(null)")} failed to load.");
        }
    }
    
    private void UnloadUIDocument()
    {
        if (uxmlLoadHandle.IsValid())
        {
            Debug.Log($"Unloading {uxmlAssetKey}");
            uxmlLoadHandle.Completed -= UxmlHandle_Completed;
            loadedAsset = null;
            Addressables.Release(uxmlLoadHandle);
            uxmlLoadHandle = default;
        }
        
        if (ussLoadHandle.IsValid())
        {
            Debug.Log($"Unloading {(ussAsset != null ? ussAsset.RuntimeKey : "(null)")}");
            ussLoadHandle.Completed -= UssHandle_Completed;
            loadedStyleSheet = null;
            Addressables.Release(ussLoadHandle);
            ussLoadHandle = default;
        }
    }
    
    private void OnDestroy()
    {
        // Clean up any remaining handles that weren't released in UnloadUIDocument.
        if (uxmlLoadHandle.IsValid())
        {
            uxmlLoadHandle.Completed -= UxmlHandle_Completed;
            Addressables.Release(uxmlLoadHandle);
            uxmlLoadHandle = default;
        }
        
        if (ussLoadHandle.IsValid())
        {
            ussLoadHandle.Completed -= UssHandle_Completed;
            Addressables.Release(ussLoadHandle);
            ussLoadHandle = default;
        }
    }

    private static Color RandomColor() {
        float nextHue = UnityEngine.Random.value;
        nextHue = (nextHue + 0.618034f) % 1f;                
        float s = UnityEngine.Random.Range(0.6f, 1f);
        float v = UnityEngine.Random.Range(0.25f, 0.55f);
        return Color.HSVToRGB(nextHue, s, v);
    }

    private static Color Lighten(Color c, float amount = 0.4f) {
        Color light = Color.Lerp(c, Color.white, amount); 
        light.a = c.a;
        return light;
    }   
}
