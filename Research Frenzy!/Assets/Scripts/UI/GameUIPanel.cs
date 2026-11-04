// GameUIPanel - goes on the GameUI object, next to its PanelRenderer
// (Source Asset = GameUI.uxml, Panel Settings = GameUIPanelSettings).
// the start screen, HUD, codex, manage firm and day summary all live in that one UXML.
// a PanelRenderer builds its UI on its own schedule and tells us through the reload callback,
// so the lookups happen there instead of in Start. this hands the root to every IGameUIPart on this object.
// the callback runs again if the UI is rebuilt (e.g. you swap the Source Asset), and every part just looks everything up again.

using UnityEngine;
using UnityEngine.UIElements;

// anything on the GameUI object that needs the elements from GameUI.uxml
public interface IGameUIPart
{
    void Bind(VisualElement root);
}

[RequireComponent(typeof(PanelRenderer))]
public class GameUIPanel : MonoBehaviour
{
    PanelRenderer panelRenderer;
    VisualElement boundRoot;
    int boundVersion = -1;

    public bool IsReady => boundRoot != null;

    void Awake()
    {
        panelRenderer = GetComponent<PanelRenderer>();
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
    }

    void OnDestroy()
    {
        if (panelRenderer != null) panelRenderer.UnregisterUIReloadCallback(OnUIReload);
    }

    void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
    {
        // same UI as last time, so skip it. otherwise every button would get its click handler twice
        if (root == boundRoot && version == boundVersion) return;
        boundRoot = root;
        boundVersion = version;

        // fill the screen, but let clicks through to the lab wherever there's no actual panel or button
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.right = 0;
        root.style.top = 0;
        root.style.bottom = 0;
        root.pickingMode = PickingMode.Ignore;

        foreach (IGameUIPart part in GetComponents<IGameUIPart>())
            part.Bind(root);
    }
}
