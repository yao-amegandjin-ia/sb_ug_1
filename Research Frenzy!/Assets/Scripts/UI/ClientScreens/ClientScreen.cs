using Clients;
using UI.ClientScreens;
using UnityEngine.UIElements;

namespace UI
{
    /// <summary>
    /// Base class for screens about a client
    /// Show client's name, problem, and closes if client leaves
    /// </summary>
    public abstract class ClientScreen : ModalScreen
    {
        private TextElement nameText;
        private TextElement problemText;

        // client whose request is open right now, null when closed
        protected Client Current { get; private set; }

        protected override void BindUI(VisualElement root)
        {
            // Label is a TextElement, so this finds either
            nameText = Find<TextElement>("client-name");
            problemText = Find<TextElement>("problem-text");
        }

        // subclasses wrap this in a public Open(...)
        protected void OpenFor(Client client)
        {
            if (!client) return;
            Current = client;
            Show();
        }

        protected override void OnShown()
        {
            if (nameText != null) nameText.text = Current.Data.Name;
            if (problemText != null) problemText.text = Current.Data.RequestText;
        }

        protected override void OnHidden()
        {
            Current = null;
        }

        // client ran out of patience (or got removed) while the screen was open
        protected override bool ShouldAutoClose()
        {
            return !Current || Current.Data.State == ClientState.LeftDissatisfied;
        }
    }
}