using UnityEngine.UIElements;

namespace UI.ClientScreens
    {
        /// <summary>
        /// Referral Screen goes on the ReferralUI object in the UI scene
        /// Opens up after "We can help" on the request Screen
        /// Selecting "We can help with that" makes the client follow the player
        /// Selecting "Wait" or "Return Tomorrow" changes ClientState
        /// </summary>
        public class ReferralScreen : ClientScreen
        {
            
            private Button helpOption;
            private Label helpDesc;
            private string helpDescDefault; 
            protected override void BindUI(VisualElement root)
            {
                base.BindUI(root);   // client name + problem text
                HookButton("close-btn", Close);
                HookButton("help-option", HelpClicked);
                HookButton("wait-option", WaitClicked);
                HookButton("tomorrow-option", TomorrowClicked);
                
                helpOption = Find<Button>("help-option");
                helpDesc = Find<Label>("help-desc");
                if (helpDesc != null) helpDescDefault = helpDesc.text;
            }

            public void Open(Client client) => OpenFor(client);
            
            protected override void OnShown()
            {
                base.OnShown();
                RefreshHelpOption();
            }
            
            // greyed out with the reason while another client follows the player
            private void RefreshHelpOption()
            {
                if (helpOption == null || !Firm) return;

                bool canHelp = Firm.CanTakeOn(Current);
                helpOption.SetEnabled(canHelp);
                if (helpDesc == null) return;

                if (!canHelp)
                    helpDesc.text = $"You're already helping {Firm.FollowingClient.Data.Name}. Finish with them first.";
                else if (Current.IsFollowing)
                    helpDesc.text = "They're already following you. Take them to a researcher.";
                else
                    helpDesc.text = helpDescDefault;
            }

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