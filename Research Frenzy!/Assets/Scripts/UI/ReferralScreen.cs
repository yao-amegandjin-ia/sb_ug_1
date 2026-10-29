using UnityEngine.UIElements;

    namespace UI
    {
        /// <summary>
        /// Referral Screen goes on the ReferralUI object in the UI scene
        /// Opens up after "We can help" on the request Screen
        /// Selecting "We can help with that" makes the client follow the player
        /// Selecting "Wait" or "Return Tomorrow" changes ClientState
        /// </summary>
        public class ReferralScreen : ClientScreen
        {
            protected override void BindUI(VisualElement root)
            {
                base.BindUI(root);   // client name + problem text
                HookButton("close-btn", Close);
                HookButton("help-option", HelpClicked);
                HookButton("wait-option", WaitClicked);
                HookButton("tomorrow-option", TomorrowClicked);
            }

            public void Open(Client client) => OpenFor(client);

            // --- options ---

            private void HelpClicked()
            {
                if (Firm) Firm.TakeOnClient(Current);
                Close();
            }

            private void WaitClicked()
            {
                if (Firm) Firm.SendToWaitingRoom(Current);
                Close();
            }

            private void TomorrowClicked()
            {
                if (Firm) Firm.SendHomeUntilTomorrow(Current);
                Close();
            }
        }
    }