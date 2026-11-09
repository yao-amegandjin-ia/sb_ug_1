// FirmController - the game rules for what the player decides about clients and researchers.
// goes on the same GameObject as GameInitializer in the Bootstrap scene.
// the screens only show things and pass on clicks. every click that changes the game calls one function here,
// so the rules can be read (and changed) in one place without any UI code around them.
// money and study results still live in GameInitializer. the HUD hears what happened through the events below.


using System;
using System.Collections.Generic;
using Researchers;
using UnityEngine;
using GameLogic.Upgrades;
using Clients;
using System.Linq;

namespace GameLogic
{
    using Assets.Scripts.Bootstrap;
    // inside the namespace so this MethodologiesEnum wins over any old global copy still in the project
    using Assets.Scripts.GameDataObjects;
    using Assets.Scripts.Interfaces;



    /// <summary>
    /// FirmController has the game logic for the game
    /// Goes on bootstrap scene with GameInitializer
    /// All UI clicks call functions here, instead of living in the UI screens
    /// </summary>
    public class FirmController : MonoBehaviour, ISceneInitializable
    {
        [Header("Rewards")]
        [SerializeField] private int bestReward = 100;
        [SerializeField] private int goodReward = 50;
        [SerializeField] private int correctRejectReward = 50;

        [Header("Researchers")]
        [Tooltip("How many of a researcher's methods can be used. The match screen's USS greys out buttons three and four, " +
                 "so this starts at 2. Set it to 4 to allow all of them.")]
        [SerializeField, Range(1, 4)] private int unlockedMethods = 2;
        
        [Header("Waiting room")]
        [Tooltip("Clients who can sit in the waiting area before any Couch upgrades.")]
        [SerializeField, Min(0)] private int baseWaitingSeats = 3;

        public event Action<int> DayEnded;

        /// <summary>A study was finished with this result. The HUD counts these.</summary>
        public event Action<StudyOutcome> StudyFinished;

        /// <summary>A request was turned down. True when that was the right call (the request can't be researched).</summary>
        public event Action<bool> RequestRejected;
        
        /// <summary>A client left dissatisfied (patience ran out, or no seat to wait in). The HUD counts these.</summary>
        public event Action<Client> ClientLost;
        private DataManager dataManager;
        //Clients from a save 
        private readonly HashSet<ClientData> trackedClients = new();

        private List<ClientData> clients;
        //private GameInitializer game;

        [Header("Lab scene components")]
        [SerializeField] private ClientGenerator clientGenerator;
        [SerializeField] private ResearcherGenerator researcherGenerator;
        [SerializeField] private ClientSpawner clientSpawner;
        [SerializeField] private ResearcherSpawner researcherSpawner;

        /// <summary>
        /// Triggered when money is added. HUD listens to this to update
        /// </summary>
        public event Action<int> MoneyEarned;

        /// <summary>
        /// Triggered when money is spent.
        /// </summary>
        public event Action<int> MoneySpent;
        private UpgradeController upgrades;    // on this same GameObject
        private Client following;  // the one client the player is helping right now
        /// <summary>The client following the player, or null. Only one at a time.</summary>
        public Client FollowingClient => following && following.IsFollowing ? following : null;

     
        public void InitializeScene(GameSystems gameSystems)
        {
            dataManager = gameSystems.DataManager;

            if (gameSystems == null || gameSystems.DataManager == null)
                throw new ArgumentNullException(nameof(gameSystems));

            if (upgrades == null) upgrades = GetComponent<UpgradeController>();
            upgrades.Initialize(gameSystems, this);
            ResolveLabComponents();

            if (researcherGenerator == null || researcherSpawner == null ||
                clientGenerator == null || clientSpawner == null)
                throw new InvalidOperationException(
                    "FirmController: assign the lab generators and spawners in the Inspector.");

            
            //Give datamanager reference to generator
            researcherGenerator.Initialize(dataManager.GetResearchers());
            //If no researchers loaded in 
            if (researcherGenerator.Researchers.Count == 0)
            {
                
                researcherGenerator.Generate();
                Debug.Log(researcherGenerator.Researchers.Count);
                Debug.Log(dataManager.Researchers.Count);
            }
            clientGenerator.Initialize(dataManager.RequestList.Requests);
            ApplyClientUpgrades();

            SetDailyClients();
            

            if (upgrades != null)
                upgrades.UpgradesChanged += ApplyClientUpgrades;

        }
        
        private void SetDailyClients()
        {

            var returningClients = dataManager.ReturningClients;
            var day = dataManager.PlayerData.CurrentDay;
            var clients = clientGenerator.GenerateDay(day, upgrades.ExtraDailyClients, dataManager.GetUsedRequestIds());
            //Not the first day and there are returning clients (if returning clients on day 1 there shouldn't be so just overwrite anyway
            if (returningClients.Count > 0 && dataManager.PlayerData.CurrentDay > 1)
            {
                var returningClientsRebuilt = new List<ClientData>();
                foreach (var client in returningClients)
                {

                    var recreatedClient = clientGenerator.CreateClientForRequest(client.RequestId);
                    returningClientsRebuilt.Add(recreatedClient);

                }
                //Append the returning clients!
                clients.AddRange(returningClientsRebuilt);
            }
            //Set the daily client list for the firm
            this.clients = clients;
        }

        private void ResolveLabComponents()
        {
            // Inspector references also support components on other lab GameObjects.
            // Fall back to GetComponent when they share Lab Systems with this controller.
            if (clientGenerator == null) clientGenerator = GetComponent<ClientGenerator>();
            if (researcherGenerator == null) researcherGenerator = GetComponent<ResearcherGenerator>();
            if (clientSpawner == null) clientSpawner = GetComponent<ClientSpawner>();
            if (researcherSpawner == null) researcherSpawner = GetComponent<ResearcherSpawner>();
        }

        private void ApplyClientUpgrades()
        {
            if (clientGenerator != null)
                clientGenerator.ExtraPatience = upgrades != null ? upgrades.ExtraPatienceSeconds : 0f;
        }
        private void OnClientSpawned(Client client)
        {
            if (client != null) TrackClient(client.Data);
        }

        //Unsubscribe from events
        private void OnDestroy()
        {
            if (upgrades != null)
                upgrades.UpgradesChanged -= ApplyClientUpgrades;
            if (clientSpawner != null)
                clientSpawner.ClientSpawned -= OnClientSpawned;
            foreach (ClientData client in trackedClients)
                client.OnStateChanged -= OnClientStateChanged;
            trackedClients.Clear();
        }

        public void TrackClient(ClientData client)
        {
            // Prevent subscribing twice to the same object.
            if (client == null || !trackedClients.Add(client))
                return;

            client.OnStateChanged += OnClientStateChanged;
        }

        private void OnClientStateChanged(ClientData client)
        {

            if (client.State == ClientState.Returning)
            {
                //Track the client for tomorrow!
            }
        }

        private void Awake()
        {
            ResolveLabComponents();

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
                RecordStudy(request.RequestId, (int)MethodologiesEnum.None, reward);
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
        
        /// <summary>Seats in the waiting area: the base amount plus one per Couch level.</summary>
        public int WaitingSeats => baseWaitingSeats + (upgrades != null ? upgrades.ExtraWaitingSeats : 0);

        /// <summary>Clients sitting in the waiting area right now.</summary>
        public int WaitingCount
        {
            get
            {
                if (clientSpawner == null) clientSpawner = FindAnyObjectByType<ClientSpawner>();
                if (clientSpawner == null) return 0;

                int count = 0;
                foreach (Client c in clientSpawner.ActiveClients)
                    if (c && c.Data != null && c.Data.State == ClientState.Waiting) count++;
                return count;
            }
        }
        
        /// <summary>True if there's a free seat, or this client is already sitting down.</summary>
        public bool CanSendToWaitingRoom(Client client)
        {
            if (!client) return false;
            return client.Data.State == ClientState.Waiting || WaitingCount < WaitingSeats;
        }

        /// <summary>
        /// "Please wait". The client goes to the waiting room, and stops following if they were.
        /// If every seat is taken, it counts as their patience running out: they leave dissatisfied.
        /// </summary>
        /// <returns>True if they sat down, false if they left because the waiting area was full.</returns>
        public bool SendToWaitingRoom(Client client)
        {
            if (!client) return false;
            StopFollowing(client);

            if (!CanSendToWaitingRoom(client))
            {
                Debug.Log($"Waiting area is full ({WaitingCount}/{WaitingSeats}), so {client.Data.Name} gave up.");
                client.Data.RunOutOfPatience();   // Client hears this and calls HandleLeftDissatisfied below
                return false;
            }

            client.Data.SendToWaitingRoom();
            return true;
        }
        
        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Every client who leaves dissatisfied ends up here, whatever the reason (the patience timer hit 0,
        /// or they were asked to wait with no free seat). Client calls it when its data fires OnLeftDissatisfied.
        /// For study scoring it counts as a rejection: the request is saved with methodology 0 (None).
        /// It pays nothing, even if the request couldn't be researched
        /// </summary>
        public void HandleLeftDissatisfied(Client client)
        {
            if (!client) return;

            RequestData request = client.Data.Request;
            if (request != null)
                RecordStudy(request.RequestId, (int)MethodologiesEnum.None, 0);
            Debug.Log($"{client.Data.Name} left dissatisfied. Saved as a rejection, +$0");

            StopFollowing(client);
            client.Leave();
            ClientLost?.Invoke(client);
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
                RecordStudy(request.RequestId, methodologyId, reward);
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
            return researcherGenerator.GetUnhired();
        }

        /// <summary>True if there's an empty cubicle. The hire tab greys out HIRE when there isn't.</summary>
        

        public bool HasFreeCubicle()
        {
            if (researcherSpawner == null) return false;
            for (int cubicle = 1; cubicle <= researcherSpawner.CubicleCount; cubicle++)
                if (researcherSpawner.IsCubicleFree(cubicle)) return true;
            return false;
        }

        /// <summary>HIRE. Puts the researcher in the first free cubicle.</summary>
        /// <returns>False if there was no free cubicle.</returns>
        public bool Hire(ResearcherData researcher)
        {
            return HireResearcher(researcher) != null;
        }

        // hires a researcher into the first free cubicle. called by the HIRE buttons on the hire screen
        public Researcher HireResearcher(ResearcherData data)
        {

            if (researcherSpawner == null || data == null) return null;

            for (int cubicle = 1; cubicle <= researcherSpawner.CubicleCount; cubicle++)
            {
                if (researcherSpawner.IsCubicleFree(cubicle))
                {
                    var researcher = researcherSpawner.Hire(data, cubicle);
                    dataManager.PlayerData

                }

            }

            Debug.Log("No free cubicles left.");
            return null;
        }



        // called by the match screen after MATCH. adds the reward and remembers which method was used

        /// <summary>
        /// Called after Match. This will add the reward and stores the study result.
        ///Also invokes the money earned event
        /// </summary>
        /// <param name="requestId">Request ID</param>
        /// <param name="methodologyId">Methodology ID</param>
        /// <param name="reward">Reward amount</param>
        public void RecordStudy(int requestId, int methodologyId, int reward)
        {
            var existing = dataManager.PlayerData.StudyResults.GetValueOrDefault(requestId, -1);

            if (existing != -1)
            {
                // same request matched again
                dataManager.PlayerData.StudyResults[requestId] = methodologyId;
                return;
            }


            dataManager.PlayerData.StudyResults.Add(requestId, methodologyId);
            dataManager.PlayerData.CurrentMoney += reward;
            dataManager.PlayerData.TotalMoneyEarned += reward;
            
           
            MoneyEarned?.Invoke(reward);
            
           

            Debug.Log($"Study recorded: request {requestId} -> methodology {methodologyId}, +${reward} " +
                      $"(current ${dataManager.PlayerData.CurrentMoney}, total ${dataManager.PlayerData.TotalMoneyEarned})");
        }
   

        /// <summary>
        /// Converts study results to JSON, so it can be saved in DB
        /// </summary>
        /// <param name="prettyPrint"></param>
        /// <returns></returns>
        public string StudyResultsToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(dataManager.PlayerData.StudyResults, prettyPrint);
        }

        
        // methodology used for a request, or -1 if it hasn't been studied
        /// <summary>
        /// Gets the methodology used for a request, or -1 if it hasn't been studied
        /// </summary>
        /// <param name="requestId"> Request ID used to find methodology</param>
        /// <returns></returns>
        public int GetStudyMethodology(int requestId)
        {
            return dataManager.PlayerData.StudyResults.GetValueOrDefault(requestId, -1);
        }

        /// <summary>
        /// Takes money out of currentMoney
        /// Used when making a purchase
        /// </summary>
        /// <returns>False (and nothing is taken) if the player doesn't have enough.</returns>
        public bool TrySpend(int amount)
        {
            var player = dataManager.PlayerData;
            if (amount < 0 || player.CurrentMoney < amount) return false;
            player.CurrentMoney -= amount;
            MoneySpent?.Invoke(amount);
            return true;
        }



        //Do work here on game starting! Note this comes if we not selected a starter yet!
        public void OnGameStarted(int selectedStarter)
        {
            //Only hire the selected starter if that screen was shown!
            if (selectedStarter != -1)
            {
                Hire(researcherGenerator.GetById(9 + selectedStarter));
                return;

            }
            else
            {
                var hired = researcherGenerator.GetHiredResearchers();
                //Hire the selected researcher!
                foreach (var h in hired)
                {
                    Hire(h);
                }
            }

            SpawnNextClient();
        }

        /// <summary>
        /// This will need to be updated in the future.
        /// Currently, this is set up for the demo to spawn in the next client from the deck
        /// </summary>
        public void SpawnNextClient()
        {
            if (clientGenerator == null || clientSpawner == null) return;
            //Reached the end of our daily clients!
            if (clients.Count == 0)
            {
                //Perform work for end of day
                EndDay();

            }
            
            var client = clients[0];
            
            
            dataManager.PlayerData.StudyResults.Add(client.RequestId, client.AssignedMethodologyId);
            //Track the clients state changes
            OnClientSpawned(clientSpawner.Spawn(client));
        }

        public void EndDay()
        {
            DayEnded?.Invoke(dataManager.PlayerData.CurrentDay);
        }

        //Day end when either: the hud fires an event for the timer expiring or 
        public void OnDayEnded(int day)
        {
            Debug.Log($"FirmController: day {day} ended.");

            // Stop admitting clients, prepare saving, etc.


        }


    }
}
