
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    
    /// <summary>
    /// Request Screen goes on the RequestUI object in the UI Scene
    /// First screen in player interaction with client, where client shows their name and problem
    /// "We can help" opens referral screen
    /// "We can't help" sends client away
    /// </summary>

    public class RequestScreen : ClientScreen
    {
        [SerializeField] private ReferralScreen referral;   // both are in the UI scene, so this can be set in the Inspector

        private void Start()
        {
            if (referral == null) referral = FindAnyObjectByType<ReferralScreen>();
        }

        protected override void BindUI(VisualElement root)
        {
            base.BindUI(root);   // client name + problem text
            HookButton("close-btn", Close);
            HookButton("accept-btn", AcceptClicked);
            HookButton("deny-btn", DenyClicked);
        }

        public void Open(Client client) => OpenFor(client);

        // "We can help" - on to the referral screen for the same client
        private void AcceptClicked()
        {
            Client client = Current;
            Close();
            if (client && referral != null) referral.Open(client);
        }

        // "We can't help" - the rules (reward, saving, the client leaving) are in FirmController.RejectRequest
        private void DenyClicked()
        {
            if (Firm) Firm.RejectRequest(Current);
            Close();
        }
    }
}
