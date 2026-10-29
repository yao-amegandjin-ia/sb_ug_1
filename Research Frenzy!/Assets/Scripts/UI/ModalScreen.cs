// ModalScreen - base class for the full-screen popups (request, referral, method match, hire).
// each popup sits on its own GameObject with a PanelRenderer:
//   Panel Settings = ModalPanelSettings, Source Asset = that screen's UXML.
// a PanelRenderer builds its UI on its own schedule and tells us through the reload callback,
// so the Q<>() lookups happen in BindUI instead of Start. the callback runs again if the UI is rebuilt
// (e.g. you swap the Source Asset or Panel Settings), and BindUI just looks everything up again.
// screens only show things and pass clicks on to the FirmController, which has the game rules.
// keep these GameObjects active. the screen hides itself with display: none, it doesn't disable anything.

using System;
using GameLogic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    
    /// <summary>
    /// Abstract Class for UI screens
    /// 
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public abstract class ModalScreen : MonoBehaviour
    {
        [Tooltip("Optional. Only needed if a <Style> path in the UXML doesn't resolve and the screen shows up unstyled.")]
        [SerializeField] private StyleSheet[] extraStyleSheets;

        [Tooltip("Colour drawn over the lab behind the screen.")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.5f);

        private PanelRenderer panelRenderer;
        private int boundVersion = -1;
        private FirmController firm;

        // the PanelRenderer's root element. null until the UI has loaded
        protected VisualElement Root { get; private set; }
        protected bool IsReady => Root != null;
        public bool IsOpen { get; private set; }

        // the game rules. it's in the Bootstrap scene, so it's found at runtime (once) instead of set in the Inspector
        protected FirmController Firm
        {
            get
            {
                if (firm == null) firm = FindAnyObjectByType<FirmController>();
                if (firm == null) Debug.LogWarning($"{GetType().Name}: no FirmController found. Is it on the GameInitializer object in the Bootstrap scene?");
                return firm;
            }
        }

        // --- what each screen fills in ---

        // look up elements and hook up buttons. Root is already set when this runs
        protected abstract void BindUI(VisualElement root);

        // the screen just became visible (or the UI reloaded while it was open). fill in the data here
        protected virtual void OnShown() { }

        // the screen just closed. forget whatever it was showing
        protected virtual void OnHidden() { }

        // checked every frame while open. return true to close (e.g. the client left)
        protected virtual bool ShouldAutoClose() => false;

        // --- Unity messages ---

        protected virtual void Awake()
        {
            panelRenderer = GetComponent<PanelRenderer>();
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        protected virtual void OnDestroy()
        {
            if (panelRenderer != null) panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        }

        protected virtual void Update()
        {
            if (IsOpen && ShouldAutoClose())
                Close();
        }

        // --- opening and closing ---

        // subclasses wrap this in their own public Open(...) so callers have to pass what the screen needs
        // ReSharper disable Unity.PerformanceAnalysis
        protected void Show()
        {
            IsOpen = true;
            if (!IsReady)
            {
                // it'll still appear once the UI loads, see OnUIReload
                Debug.LogWarning($"{GetType().Name}: opened before its UI loaded. Check the PanelRenderer has a Source Asset and Panel Settings.");
                
                return;
            }

            ApplyVisibility();
            OnShown();
        }

        public void Close()
        {
            IsOpen = false;
            if (IsReady) ApplyVisibility();
            OnHidden();
        }

        // --- PanelRenderer ---

        // the version only changes when the UI is really rebuilt, so skip repeat calls.
        // otherwise every button would get its click handler added twice
        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            if (root == Root && version == boundVersion) return;
            Root = root;
            boundVersion = version;

            SetUpOverlay(root);
            BindUI(root);

            ApplyVisibility();
            if (IsOpen) OnShown();
        }

        // cover the whole screen, dim the lab behind it and center the card
        private void SetUpOverlay(VisualElement root)
        {
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.style.justifyContent = Justify.Center;
            root.style.alignItems = Align.Center;
            root.style.backgroundColor = dimColor;

            if (extraStyleSheets == null) return;
            foreach (StyleSheet sheet in extraStyleSheets)
            {
                if (sheet != null && !root.styleSheets.Contains(sheet))
                    root.styleSheets.Add(sheet);
            }
        }

        private void ApplyVisibility()
        {
            Root.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // --- helpers for BindUI ---

        // Q<T>(name) that says which UXML is missing the element, instead of a NullReferenceException later
        protected T Find<T>(string elementName) where T : VisualElement
        {
            T element = Root.Q<T>(elementName);
            if (element == null)
                Debug.LogWarning($"{GetType().Name}: no {typeof(T).Name} named '{elementName}' in {SourceName}.");
            return element;
        }

        protected void HookButton(string buttonName, Action onClick)
        {
            Button button = Find<Button>(buttonName);
            if (button != null) button.clicked += onClick;
        }

        private string SourceName => panelRenderer != null && panelRenderer.visualTreeAsset != null
            ? panelRenderer.visualTreeAsset.name
            : "the UXML";
    }
}
