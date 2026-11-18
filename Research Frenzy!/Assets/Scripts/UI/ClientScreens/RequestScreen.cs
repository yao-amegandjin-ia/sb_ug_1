using DataLogging;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.ClientScreens
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
            HookButton("close-btn", CloseClicked);
            HookButton("accept-btn", AcceptClicked);
            HookButton("deny-btn", DenyClicked);
        }

        // opening this screen is the player greeting the client
        public void Open(Client client)
        {
            OpenFor(client);
            if (client)
            {
                ClientLog.Greeted(client.Data);          // DATA LOG: client_greeted (wait stops / greeting starts)
                AnswerLog.QuestionPosted(client.Data);   // DATA LOG: question_posted
            }
        }

        // X without answering
        private void CloseClicked()
        {
            if (Current) AnswerLog.QuestionDismissed(Current.Data);   // DATA LOG: question_dismissed
            Close();
        }

        // "We can help" - on to the referral screen for the same client
        private void AcceptClicked()
        {
            Client client = Current;
            if (client) AnswerLog.QuestionAnswered(client.Data, true);   // DATA LOG: question_answered ("can_help")
            Close();
            if (client && referral != null) referral.Open(client);
        }

        // "We can't help" - the rules (reward, saving, the client leaving) are in FirmController.RejectRequest
        private void DenyClicked()
        {
            if (Current) AnswerLog.QuestionAnswered(Current.Data, false);   // DATA LOG: question_answered ("cannot_help")
            if (Firm) Firm.RejectRequest(Current);
            Close();
        }
    }
}
