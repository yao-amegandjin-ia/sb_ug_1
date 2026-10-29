// FirmController - the game rules for what the player decides about clients and researchers.
// goes on the same GameObject as GameInitializer in the Bootstrap scene.
// the screens only show things and pass on clicks. every click that changes the game calls one function here,
// so the rules can be read (and changed) in one place without any UI code around them.
// money and study results still live in GameInitializer. the HUD hears what happened through the events below.


using System;
using System.Collections.Generic;
using Researchers;
using UnityEngine;

namespace GameLogic
{
    // inside the namespace so this MethodologiesEnum wins over any old global copy still in the project
    using Assets.Scripts.GameDataObjects;

    /// <summary>
    /// FirmController has the game logic for the game
    /// Goes on bootstrap scene with GameInitializer
    /// All UI clicks call functions here, instead of living in the UI screens
    /// </summary>
    public class FirmController : MonoBehaviour
    {
        [Header("Rewards")]
        [SerializeField] private int bestReward = 100;
        [SerializeField] private int goodReward = 50;
        [SerializeField] private int correctRejectReward = 50;

        [Header("Researchers")]
        [Tooltip("How many of a researcher's methods can be used. The match screen's USS greys out buttons three and four, " +
                 "so this starts at 2. Set it to 4 to allow all of them.")]
        [SerializeField, Range(1, 4)] private int unlockedMethods = 2;

        /// <summary>A study was finished with this result. The HUD counts these.</summary>
        public event Action<StudyOutcome> StudyFinished;

        /// <summary>A request was turned down. True when that was the right call (the request can't be researched).</summary>
        public event Action<bool> RequestRejected;

        private GameInitializer game;
        private ResearcherGenerator researcherGenerator;   // in the Research_Lab scene, so found when first needed
        
        private Client following;                          // the one client the player is helping right now
        /// <summary>The client following the player, or null. Only one at a time.</summary>
        public Client FollowingClient => following && following.IsFollowing ? following : null;

        private void Awake()
        {
            game = GetComponent<GameInitializer>();
            if (game == null) game = FindAnyObjectByType<GameInitializer>();
            if (game == null) Debug.LogError("FirmController: no GameInitializer found. Put this on the GameInitializer object.");
        }

        // Request Screen

        /// <summary>
        /// "We can't help". Pays correctRejectReward if the request really can't be researched,
        /// saves the request with methodology 0 (None) and sends the client away.
        /// </summary>
        /// <returns>True if turning it down was correct.</returns>
        public bool RejectRequest(Client client)
        {
            if (!client) return false;

            RequestData request = client.Data.Request;
            bool correct = StudyScoring.IsCorrectReject(request);
            int reward = correct ? correctRejectReward : 0;

            if (request != null)
                game.RecordStudy(request.RequestId, (int)MethodologiesEnum.None, reward);
            Debug.Log($"{client.Data.Name} denied: {(correct ? "correct" : "wrong, this one was researchable")}, +${reward}");

            client.Data.Complete();   // "request was rejected"
            client.Leave();

            RequestRejected?.Invoke(correct);
            return correct;
        }

        // Referral Screen

        // --- referral screen ---
 
        /// <summary>True if the player can take this client on: nobody else is following, or it's this client already.</summary>
        public bool CanTakeOn(Client client)
        {
            return FollowingClient == null || FollowingClient == client;
        }
 
        /// <summary>
        /// "We can help with that". The client follows the player until they're matched with a researcher.
        /// Only one client can follow at a time.
        /// </summary>
        /// <returns>False if someone else is already following.</returns>
        public bool TakeOnClient(Client client)
        {
            if (!client) return false;
            if (!CanTakeOn(client))
            {
                Debug.Log($"Already helping {FollowingClient.Data.Name}, so {client.Data.Name} can't follow too.");
                return false;
            }
 
            if (!client.IsFollowing) client.FollowPlayer();
            following = client;
            return true;
        }

        /// <summary>"Please wait". The client goes to the waiting room, and stops following if they were.</summary>
        public void SendToWaitingRoom(Client client)
        {
            if (!client) return;
            StopFollowing(client);
            client.Data.SendToWaitingRoom();
        }
 
        /// <summary>"Come back tomorrow". The client leaves and returns the next day.</summary>
        public void SendHomeUntilTomorrow(Client client)
        {
            if (!client) return;
            StopFollowing(client);
            client.Data.AskToReturnTomorrow();
            client.Leave();
        }
 
        // frees the "one client at a time" slot if this was the client following
        private void StopFollowing(Client client)
        {
            if (client != following) return;
            client.StopFollowing();
            following = null;
        }

        //Method Match Screen

        /// <summary>How many of this researcher's methods can be used. The same for everyone until levels exist.</summary>
        public int UnlockedMethodCount(ResearcherData researcher)
        {
            return unlockedMethods;
        }

        /// <summary>
        /// MATCH. Grades the methodology, pays the reward, saves {request ID : methodology ID}
        /// and finishes the client (for now they leave straight away, there's no research timer yet).
        /// </summary>
        public StudyOutcome CompleteStudy(Client client, Researcher researcher, int methodologyId)
        {
            if (!client || !researcher) return StudyOutcome.Wrong;

            RequestData request = client.Data.Request;
            MethodologiesEnum picked = (MethodologiesEnum)methodologyId;
            StudyOutcome outcome = StudyScoring.Score(request, picked);
            int reward = RewardFor(outcome);

            if (request != null)
                game.RecordStudy(request.RequestId, methodologyId, reward);
            Debug.Log($"{client.Data.Name} matched with {researcher.Data.Name} using {picked} ({outcome}): +${reward}");

            client.Data.AssignTo(researcher.Data.Id, methodologyId);
            client.Data.Complete();
            client.Leave();

            StudyFinished?.Invoke(outcome);
            return outcome;
        }

        private int RewardFor(StudyOutcome outcome)
        {
            switch (outcome)
            {
                case StudyOutcome.Best: return bestReward;
                case StudyOutcome.Good: return goodReward;
                default: return 0;
            }
        }

        // --- hire screen ---

        /// <summary>Researchers the player can hire right now (everyone not hired yet).</summary>
        public IReadOnlyList<ResearcherData> GetHireCandidates()
        {
            if (researcherGenerator == null) researcherGenerator = FindAnyObjectByType<ResearcherGenerator>();
            if (researcherGenerator == null)
            {
                Debug.LogWarning("FirmController: no ResearcherGenerator found. Is it in the Research_Lab scene?");
                return Array.Empty<ResearcherData>();
            }
            return researcherGenerator.GetUnhired();
        }

        /// <summary>HIRE. Puts the researcher in the first free cubicle.</summary>
        /// <returns>False if there was no free cubicle.</returns>
        public bool Hire(ResearcherData researcher)
        {
            return game.HireResearcher(researcher) != null;
        }
    }
}
