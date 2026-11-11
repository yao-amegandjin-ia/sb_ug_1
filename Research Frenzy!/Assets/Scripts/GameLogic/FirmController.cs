// FirmController - the game rules for what the player decides about clients and researchers.
// goes on the same GameObject as GameInitializer in the Bootstrap scene.
// the screens only show things and pass on clicks. every click that changes the game calls one function here,
// so the rules can be read (and changed) in one place without any UI code around them.
// money and study results live in the DataManager. the HUD and DaySummary hear what happened through the events below.
// it also owns the day: the timer, which day it is, starting the next one and ending this one.
// the HUD only shows TimeLeft / CurrentDay, and DaySummary opens on DayEnded and calls NextDay.


using System;
using System.Collections.Generic;
using Researchers;
using UnityEngine;
using GameLogic.Upgrades;
using Clients;
using System.Linq;
using GameDataObjects;
using Interfaces;
using Bootstrap;

namespace GameLogic
{
    // RequestData lives here


    /// <summary>
    /// FirmController has the game logic for the game
    /// Goes on bootstrap scene with GameInitializer
    /// All UI clicks call functions here, instead of living in the UI screens
    /// </summary>
    public class FirmController : MonoBehaviour, ISceneInitializable
    {
        [Header("Rewards")]
        [Tooltip("Money for a match, by whether step one (approach) was right and how good step two (methodology) was.")]
        [SerializeField] private MatchRewards matchRewards = new MatchRewards();
        [Tooltip("\"We can't help\" on a request that can't be researched. Turning away a researchable one pays 0.")]
        [SerializeField] private int correctRejectReward = 50;

        [Header("Researchers")]
        [Tooltip("How many of a researcher's methods can be used. The match screen's USS greys out buttons three and four, " +
                 "so this starts at 2. Set it to 4 to allow all of them.")]
        [SerializeField, Range(1, 4)] private int unlockedMethods = 2;
        
        [Header("Day")]
        [Tooltip("Seconds in a day before Clock upgrades.")]
        [SerializeField, Min(1f)] private float baseDayLength = 180f;
        [Tooltip("Days in a full game. The day summary after the last one is the end of the game.")]
        [SerializeField, Min(1)] private int totalDays = 7;

        [Header("Waiting room")]
        [Tooltip("Clients who can sit in the waiting area before any Couch upgrades.")]
        [SerializeField, Min(0)] private int baseWaitingSeats = 3;

        /// <summary>A day began (the timer is running). Gives the day number. The HUD resets its "today" counts here.</summary>
        public event Action<int> DayStarted;

        /// <summary>
        /// The day is over: the timer ran out, or there are no clients left today. Gives the day number.
        /// Fires once per day. DaySummary opens on this.
        /// </summary>
        public event Action<int> DayEnded;

        /// <summary>A study was finished with this result. The HUD counts these.</summary>
        public event Action<StudyOutcome> StudyFinished;

        /// <summary>A request was turned down. True when that was the right call (the request can't be researched).</summary>
        public event Action<bool> RequestRejected;
        
        /// <summary>A client left dissatisfied (patience ran out, or no seat to wait in). The HUD counts these.</summary>
        public event Action<Client> ClientLost;

        /// <summary>
        /// Any client finished (matched, turned away or lost), with their request and the methodology picked.
        /// DaySummary builds its list of today's clients from this.
        /// </summary>
        public event Action<ClientResult> ClientResolved;
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

            
            
            
            //If no researchers loaded in 
            if (researcherGenerator.Researchers.Count == 0)
            {
                
                var researchers = researcherGenerator.Generate();
                dataManager.SetResearchers(researchers);
                researcherGenerator.Initialize(dataManager.Researchers);

            }
            clientGenerator.Initialize(dataManager.GetRequests());
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
                RecordStudy(request.RequestId, ResearchApproach.None, MethodLibrary.NoneId, reward);
            Debug.Log($"{client.Data.Name} denied: {(correct ? "correct" : "wrong, this one was researchable")}, +${reward}");

            client.Data.Complete();   // "request was rejected"
            client.Leave();

            RequestRejected?.Invoke(correct);
            ClientResolved?.Invoke(new ClientResult
            {
                Request = request,
                MethodologyId = MethodLibrary.NoneId,
                Outcome = correct ? ClientOutcome.CorrectReject : ClientOutcome.WrongReject,
                Reward = reward
            });
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
                RecordStudy(request.RequestId, ResearchApproach.None, MethodLibrary.NoneId, 0);
            Debug.Log($"{client.Data.Name} left dissatisfied. Saved as a rejection, +$0");

            StopFollowing(client);
            client.Leave();
            ClientLost?.Invoke(client);
            ClientResolved?.Invoke(new ClientResult { Request = request, MethodologyId = MethodLibrary.NoneId, Outcome = ClientOutcome.Lost });
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
        /// MATCH. Grades both steps (the approach, then the methodology), pays from the matchRewards table,
        /// saves {request ID : methodology ID} and finishes the client (they leave straight away, there's no research timer yet).
        /// </summary>
        public MatchGrade CompleteStudy(Client client, Researcher researcher, ResearchApproach approach, int methodologyId)
        {
            if (!client || !researcher) return new MatchGrade(false, StudyOutcome.Wrong);

            RequestData request = client.Data.Request;
            MatchGrade grade = StudyScoring.Grade(request, approach, methodologyId);   // methodology IDs come from the methodologies spreadsheet
            int reward = matchRewards.For(grade);

            if (request != null)
                RecordStudy(request.RequestId, approach, methodologyId, reward);
            Debug.Log($"{client.Data.Name} matched with {researcher.Data.Name}: {approach} / {MethodLibrary.GetName(methodologyId)} ({grade}): +${reward}");

            client.Data.AssignTo(researcher.Data.Id, methodologyId);
            client.Data.Complete();
            client.Leave();

            StudyFinished?.Invoke(grade.Method);
            ClientResolved?.Invoke(new ClientResult
            {
                Request = request,
                Approach = approach,
                ApproachCorrect = grade.ApproachCorrect,
                MethodologyId = methodologyId,
                Outcome = ClientResult.FromStudy(grade.Method),
                Reward = reward
            });
            return grade;
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
                // stop at the first free cubicle. without the return, the same researcher was hired into every free one
                if (researcherSpawner.IsCubicleFree(cubicle))
                    return researcherSpawner.Hire(data, cubicle);

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
        public void RecordStudy(int requestId, ResearchApproach approach, int methodologyId, int reward)
        {
            // the HUD's "earned today" (shown in DaySummary) listens to MoneyEarned, so it has to fire here.
            // TryRecordStudy returns false when this request was already paid for, and then nothing was earned
            if (dataManager.TryRecordStudy(requestId, approach, methodologyId, reward) && reward > 0)
                MoneyEarned?.Invoke(reward);
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

        /// <summary>The approach picked for a request (step one), or None if it hasn't been studied or was turned away.</summary>
        public ResearchApproach GetStudyApproach(int requestId)
        {
            return (ResearchApproach)dataManager.PlayerData.StudyApproaches.GetValueOrDefault(requestId, 0);
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
            dataManager.DecreaseMoney(amount);
            MoneySpent?.Invoke(amount);
            return true;
        }

        /// <summary>
        /// Adds money that isn't from a study (the quiz rewards).
        /// Fires MoneyEarned so the HUD and the day summary update.
        /// </summary>
        public void AddMoney(int amount)
        {
            if (amount <= 0) return;
            dataManager.IncreaseMoney(amount);
            MoneyEarned?.Invoke(amount);
        }



        //Do work here on game starting! Note this comes if we not selected a starter yet!
        public void OnGameStarted(int selectedStarter)
        {
            //Only hire the selected starter if that screen was shown!
            if (selectedStarter != -1)
            {
                Hire(researcherGenerator.GetById(9 + selectedStarter));
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

            StartDay();
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
                return;   // without this, clients[0] below throws on an empty list
            }
            
            var client = clients[0];
            clients.RemoveAt(0);   // so the next call sends in the next client, not the same one again
            
            
            //Track the clients state changes
            OnClientSpawned(clientSpawner.Spawn(client));
        }

        // ---------- the day ----------

        private float timeLeft;
        private bool dayRunning;

        /// <summary>Today's length: the base length plus the Clock upgrade. A Clock bought during the day counts from the next day.</summary>
        public float DayLength => baseDayLength + (upgrades != null ? upgrades.ExtraDaySeconds : 0f);

        /// <summary>Seconds left today. Before the first day starts it's the full length, after the day ends it's 0.</summary>
        public float TimeLeft => timeLeft;

        /// <summary>True while the timer is counting down.</summary>
        public bool IsDayRunning => dayRunning;

        /// <summary>The day being played, from the save, so it carries over between sessions.</summary>
        public int CurrentDay => dataManager != null ? dataManager.PlayerData.CurrentDay : 1;

        public int TotalDays => totalDays;

        public bool IsLastDay => CurrentDay >= totalDays;

        private void Start()
        {
            timeLeft = DayLength;   // so the HUD shows the full day before Start is pressed
        }

        // the timer. Time.deltaTime is the seconds since the last frame, so this counts down in real seconds
        private void Update()
        {
            if (!dayRunning) return;

            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0f)
                EndDay();
        }

        /// <summary>Starts the timer for the current day. Called by OnGameStarted and NextDay.</summary>
        private void StartDay()
        {
            timeLeft = DayLength;
            dayRunning = true;
            Debug.Log($"FirmController: day {CurrentDay} started ({DayLength:0}s).");
            DayStarted?.Invoke(CurrentDay);
        }

        /// <summary>
        /// Ends today: stops the timer, saves and tells everyone. Called when the timer runs out, and by
        /// SpawnNextClient when there are no clients left. Does nothing if the day already ended,
        /// so both happening doesn't open the day summary twice.
        /// </summary>
        public void EndDay()
        {
            if (!dayRunning) return;
            dayRunning = false;
            timeLeft = 0f;

            int day = CurrentDay;
            Debug.Log($"FirmController: day {day} ended.");
            DayEnded?.Invoke(day);
            SaveAfterDay();
        }

        // async void: Unity can't wait for this, so it runs alongside the game. fine for a save that nothing waits on
        private async void SaveAfterDay()
        {
            await dataManager.SaveGame();
        }

        /// <summary>
        /// Moves to the next day: day number +1 (saved), a fresh set of clients, the timer starts and the
        /// first client comes in. DaySummary calls this when the player presses Continue.
        /// </summary>
        public void NextDay()
        {
            if (dayRunning) return;   // only between days
            if (IsLastDay)
            {
                Debug.Log("FirmController: that was the last day.");
                return;
            }

            dataManager.AdvanceDay();
            SetDailyClients();   // uses the new day number and skips requests already answered
            StartDay();
            SpawnNextClient();
        }


    }
}
