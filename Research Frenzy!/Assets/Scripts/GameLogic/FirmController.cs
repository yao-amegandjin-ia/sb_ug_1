// FirmController - the game rules for what the player decides about clients and researchers.
// goes on the same GameObject as GameInitializer in the Bootstrap scene.
// the screens only show things and pass on clicks. every click that changes the game calls one function here,
// so the rules can be read (and changed) in one place without any UI code around them.
// money and study results live in the DataManager. the HUD and DaySummary hear what happened through the events below.
// it also owns the day: the timer, which day it is, starting the next one and ending this one.
// the HUD only shows TimeLeft / CurrentDay, and DaySummary opens on DayEnded and calls NextDay.
// MATCH starts a research session (StartResearch), the researcher
// and client are busy until the timer ends (HandleResearchFinished), then the player clicks the client to collect
// the money and they leave (CollectResearch).

// clients come in on a timer (Seconds Between Clients), one at a time at the entrance. Nobody greets
// them for Entrance Wait seconds -> they go to the waiting room by themselves, and the next one can come in.

// day 1 of a new game is scripted around the starter: an unresearchable request first, then two the
// starter can answer with their best method.

// SaveGame() is called by the day summary's two buttons. OnGameStarted starts a NEW game,
// ContinueSavedGame carries on from the save (the save is made at the day summary, so it carries on from there).


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
using DataLogging;

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
        [Tooltip("XP a researcher gets when the player collects a study, by how it was matched. " +
                 "2 XP = level 2, 5 = level 3, 10 = level 4 (ResearcherLevels.cs).")]
        [SerializeField] private MatchExperience matchExperience = new MatchExperience();
        
        [Tooltip("Money taken when a researcher is hired in the shop. The starting researcher picked on the start screen is free.")]
        [SerializeField, Min(0)] private int hireCost = 200;
        
        [Header("Research sessions")]
        [Tooltip("Seconds a client spends with a researcher, before speed. Speed divides it: speed 2 = half as long.")]
        [SerializeField, Min(0f)] private float baseResearchSeconds = 20f;
        [Tooltip("Extra money per quality level above 1. 0.25 = quality 1 pays x1, quality 2 x1.25, quality 3 x1.5.")]
        [SerializeField, Min(0f)] private float qualityBonusPerLevel = 0.25f;
        
        [Header("Day")]
        [Tooltip("Seconds in a day before Clock upgrades.")]
        [SerializeField, Min(1f)] private float baseDayLength = 180f;
        [Tooltip("Days in a full game. The day summary after the last one is the end of the game.")]
        [SerializeField, Min(1)] private int totalDays = 7;

        [Header("Waiting room")]
        [Tooltip("Clients who can sit in the waiting area before any Couch upgrades.")]
        [SerializeField, Min(0)] private int baseWaitingSeats = 3;
        
        [Header("Clients coming in")]
        [Tooltip("Seconds between one client walking in and the next. The next one also waits until the entrance is " +
                 "free (the client there was helped, or went to the waiting room).")]
        [SerializeField, Min(0f)] private float secondsBetweenClients = 12f;
        [Tooltip("Seconds a client waits at the entrance to be greeted. Then they go to the waiting room by themselves " +
                 "(and leave dissatisfied if every seat is taken). Their patience only starts in the waiting room.")]
        [SerializeField, Min(0f)] private float entranceWaitSeconds = 15f;


        [Header("First day")]
        [Tooltip("Ticked: day 1 of a new game starts with a request that can't be researched, then two the starter " +
                 "researcher can answer with their BEST method. Untick to get a normal random day 1 (handy for testing).")]
        [SerializeField] private bool scriptedFirstDay = true;

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
        
        /// <summary>A researcher reached a new level (once per level). DaySummary lists these at the end of the day.</summary>
        public event Action<ResearcherLevelUp> ResearcherLeveledUp;
        
        /// <summary>MATCH was pressed and a research session started.</summary>
        public event Action<ResearchSession> ResearchStarted;
        /// <summary>A research session's timer ended. The researcher is free, the client waits to be collected.</summary>
        public event Action<ResearchSession> ResearchFinished;

        // every client with a researcher or waiting to be collected, keyed by their data
        private readonly Dictionary<ClientData, ResearchSession> sessions = new();
        private bool endingDay;   // true while EndDay wraps up the sessions, so the log can say why they ended early
        private DataManager dataManager;
        //Clients from a save
        private readonly HashSet<ClientData> trackedClients = new();

        // the client standing at the entrance (null = free), and the time since the last one came in
        private Client atEntrance;
        private float sinceLastEntry;

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

            //Connect the researchers
            researcherGenerator.Initialize(dataManager.Researchers);

            //If no researchers loaded in (this means we will need to show the start screen so probably move that check from UI to here)
            if (researcherGenerator.Researchers.Count == 0)
            {
                var researchers = researcherGenerator.Generate();
                dataManager.UpdateResearchers(researchers);
            }
            clientGenerator.Initialize(dataManager.GetRequests());
            ApplyClientUpgrades();
            

            if (upgrades != null)
                upgrades.UpgradesChanged += ApplyClientUpgrades;

            // DATA LOG: every logged event gets the day number and the time left on the day timer
            GameLog.AttachDayClock(() => CurrentDay, () => timeLeft);
        }
        
        private void SetDailyClients(bool fromLoad, ResearcherData starter = null)
        {

            var returningClients = dataManager.ReturningClients;
            var day = dataManager.PlayerData.CurrentDay;
            List<ClientData> clients;
            //Send in the usedrequestids, otherwise those are already known
            
            //  the scripted day 1. Anything else is the normal shuffled deck, like before
            if (day == 1 && scriptedFirstDay && starter != null)
                clients = clientGenerator.GenerateFirstDay(starter, upgrades.ExtraDailyClients, dataManager.GetUsedRequestIds());
            else
                clients = clientGenerator.GenerateDay(day, upgrades.ExtraDailyClients, dataManager.GetUsedRequestIds());

            
          
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
            
            //TODO: DELETE THIS BLOCK WHEN ENOUGH REQUESTS
            // TEMP REPEATS (new): not enough new requests left for today, so answered ones come back (wrong answers first).
            // Remove this block (and ClientGenerator.GenerateRepeats) once the spreadsheet has enough requests
            int wanted = clientGenerator.CalculateClientCount(day, upgrades.ExtraDailyClients);
            int newClients = clients.Count - (returningClients.Count > 0 && day > 1 ? returningClients.Count : 0);
            if (clientGenerator.RepeatRequestsWhenOutOfNew && newClients < wanted)
            {
                List<int> alreadyToday = clients.Where(c => c != null).Select(c => c.RequestId).ToList();
                List<ClientData> repeats = clientGenerator.GenerateRepeats(wanted - newClients,
                    dataManager.PlayerData.StudyResults.Keys, alreadyToday, WasAnsweredRight);
                if (repeats.Count > 0)
                    Debug.Log($"FirmController: only {newClients} new requests left for day {day}, so {repeats.Count} answered ones come back.");
                clients.AddRange(repeats);
            }
            
            //Update the daily client list for the firm
            dataManager.UpdateDailyClients(clients, true);
        }

        // TEMP REPEATS (new): was the saved answer for this request the right one? Right = the best methodology with the
        // right approach, or "We can't help" on a request that can't be researched. Used to put wrong answers first
        private bool WasAnsweredRight(int requestId)
        {
            RequestData request = dataManager.GetRequests().FirstOrDefault(r => r.RequestId == requestId);
            if (request == null || !dataManager.PlayerData.StudyResults.TryGetValue(requestId, out int methodId)) return false;
            if (!request.IsResearchable) return methodId == MethodLibrary.NoneId;
            ResearchApproach approach = GetStudyApproach(requestId);
            return StudyScoring.Grade(request, approach, methodId).Method == StudyOutcome.Best &&
                   StudyScoring.IsCorrectApproach(request, approach);
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

        /// <summary>
        /// Add the client to the tracked clients list if not null and subscribe it to state changes event!
        /// </summary>
        /// <param name="client"></param>
        public void TrackClient(ClientData client)
        {
            // Prevent subscribing twice to the same object.
            if (client == null || !trackedClients.Add(client))
                return;

            client.OnStateChanged += OnClientStateChanged;
        }
        public void UntrackClient(ClientData client)
        {
            if (client == null || !trackedClients.Remove(client))
                return;

            client.OnStateChanged -= OnClientStateChanged;
        }

        //Currently only used to check for returning client state
        private void OnClientStateChanged(ClientData client)
        {
            if (client.State == ClientState.Returning)
            {
                dataManager.AddReturningClient(client);
                
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
            ClientLog.TurnedAway(client.Data, correct, reward);   // DATA LOG: client_leave (turned_away)

            client.Data.Complete();   // "request was rejected"
            UntrackClient(client.Data);
            client.Leave();

            RequestRejected?.Invoke(correct);
            ClientResolved?.Invoke(new ClientResult
            {
                Request = request,
                MethodologyId = MethodLibrary.NoneId,
                Outcome = correct ? ClientOutcome.CorrectReject : ClientOutcome.WrongReject,
                Reward = reward
            });
            // CLIENT ENTRANCE (changed): SpawnNextClient() was called here. Clients now come in on a timer (Update)
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
 
            if (!client.IsFollowing)
            {
                client.FollowPlayer();
                ClientLog.Following(client.Data);   // DATA LOG: client_following
            }
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
        // CLIENT ENTRANCE (changed): byEntranceTimeout is new, only for the data log (HandleEntranceWaitOver passes true)
        public bool SendToWaitingRoom(Client client, bool byEntranceTimeout = false)
        {
            if (!client) return false;
            StopFollowing(client);
            string sentBy = byEntranceTimeout ? "entrance_timeout" : "player";

            if (!CanSendToWaitingRoom(client))
            {
                Debug.Log($"Waiting area is full ({WaitingCount}/{WaitingSeats}), so {client.Data.Name} gave up.");
                ClientLog.SentToWaiting(client.Data, false, WaitingCount, WaitingSeats, sentBy);   // DATA LOG (client_leave follows)
                client.Data.RunOutOfPatience();   // Client hears this and calls HandleLeftDissatisfied below
                return false;
            }

            client.Data.SendToWaitingRoom();
            ClientLog.SentToWaiting(client.Data, true, WaitingCount, WaitingSeats, sentBy);   // DATA LOG: client_sent_to_waiting
            return true;
        }

        // CLIENT ENTRANCE (new)
        /// <summary>
        /// Nobody greeted this client at the entrance in time (Client calls this when its data fires OnEntranceWaitOver).
        /// Same as the player picking "Please wait": they go to the waiting room and their patience starts,
        /// or they leave dissatisfied if every seat is taken. The entrance is then free for the next client.
        /// </summary>
        public void HandleEntranceWaitOver(Client client)
        {
            if (!client || client.Data == null || !client.Data.IsAtEntrance) return;
            Debug.Log($"Nobody greeted {client.Data.Name} for {entranceWaitSeconds:0}s, so they went to the waiting room.");
            SendToWaitingRoom(client, byEntranceTimeout: true);
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
            ClientLog.Lost(client.Data);   // DATA LOG: client_leave (patience_ran_out / no_seat_in_waiting_room)

            StopFollowing(client);
            UntrackClient(client.Data);
            client.Leave();
            ClientLost?.Invoke(client);
            ClientResolved?.Invoke(new ClientResult { Request = request, MethodologyId = MethodLibrary.NoneId, Outcome = ClientOutcome.Lost });
            // CLIENT ENTRANCE (changed): SpawnNextClient() was called here. Clients now come in on a timer (Update)
        }
 
        /// <summary>"Come back tomorrow". The client leaves and returns the next day.</summary>
        public void SendHomeUntilTomorrow(Client client)
        {
            if (!client) return;
            StopFollowing(client);
            ClientLog.SentHome(client.Data);   // DATA LOG: client_leave (return_tomorrow)
            client.Data.AskToReturnTomorrow();
            UntrackClient(client.Data);
            client.Leave();
            // CLIENT ENTRANCE (changed): SpawnNextClient() was called here. Clients now come in on a timer (Update)
        }

        
        // frees the "one client at a time" slot if this was the client following
        private void StopFollowing(Client client)
        {
            if (client == null || client != following) return;
            client.StopFollowing();
            following = null;
        }

        //Method Match Screen
        
        /// <summary>How many of this researcher's methods can be used: one per level. The match screen greys out the rest.</summary>
        public int UnlockedMethodCount(ResearcherData researcher)
        {
            return researcher != null ? researcher.UnlockedMethodCount : 0;
        }
        
        /// <summary>
        /// MATCH. Grades both steps (the approach, then the methodology), works out the money (matchRewards table,
        /// then the researcher's quality) and starts a research session. The researcher and the client are busy
        /// until the timer ends; the length is baseResearchSeconds shortened by the researcher's speed.
        /// </summary>
        public MatchGrade StartResearch(Client client, Researcher researcher, ResearchApproach approach, int methodologyId)
        {
            if (!client || !researcher) return new MatchGrade(false, StudyOutcome.Wrong);

            ResearcherData researcherData = researcher.Data;
            
            if (!researcherData.CanUseMethodology(methodologyId))
            {
                Debug.LogWarning($"{researcherData.Name} hasn't unlocked {MethodLibrary.GetName(methodologyId)} yet (level {researcherData.Level}).");
                return new MatchGrade(false, StudyOutcome.Wrong);
            }

            RequestData request = client.Data.Request;
            MatchGrade grade = StudyScoring.Grade(request, approach, methodologyId);   // methodology IDs come from the methodologies spreadsheet
            int baseReward = matchRewards.For(grade);
            int experience = matchExperience.For(grade);  

 
            float duration = ResearchSession.DurationFor(researcherData, baseResearchSeconds);
            float qualityMultiplier = ResearchSession.QualityMultiplier(researcherData, qualityBonusPerLevel);   // Nov 21: 1 / 1.25 / 1.5
            int reward = ResearchSession.RewardFor(baseReward, qualityMultiplier);
            
            //DATA LOG
            List<object> possibleResearchers = AnswerLog.FindCorrectResearchers(request, researcherGenerator.GetHiredResearchers());

            // RESEARCH TIMER: the researcher is now Busy until the player collects the result (CollectResearch).
            if (!researcherData.AssignClient(client.Data.Id))
            {
                Debug.LogWarning($"{researcherData.Name} couldn't take {client.Data.Name} (state: {researcherData.CurrentState}).");
                return grade;
            }

            ResearchSession session = new ResearchSession
            {
                Client = client,
                Researcher = researcher,
                ResearcherData = researcherData,
                Approach = approach,
                MethodologyId = methodologyId,
                Grade = grade,
                BaseReward = baseReward,
                Reward = reward,
                Duration = duration,
                Experience = experience   // RESEARCHER XP (new)
            };
            sessions[client.Data] = session;

            Debug.Log($"{client.Data.Name} matched with {researcherData.Name}: {approach} / {MethodLibrary.GetName(methodologyId)} ({grade}). " +
                      $"Research takes {duration:0.#}s, pays ${reward} (base ${baseReward} x {qualityMultiplier:0.##} for quality {researcherData.Quality}), " +
                      $"+{experience} XP.");

            // DATA LOG: match_answered (AnswerLog), then client_matched (ClientLog: greeting stops / researcher starts)
            AnswerLog.MatchAnswered(client.Data, researcherData, approach, methodologyId, grade, reward, possibleResearchers);
            ClientLog.Matched(client.Data, researcherData, grade, reward, duration);

            // RESEARCH TIMER: the client stops following and stays by the researcher.
            // StopFollowing also frees the "one client at a time" slot, so the player can take on the next client
            StopFollowing(client);
            client.Data.StartResearch(researcherData.Id, methodologyId, duration);   // state -> Assisted, the timer starts

            ResearchStarted?.Invoke(session);

            // CLIENT ENTRANCE (changed): the next client used to come in here (SpawnNextClient). Clients now come in on
            // a timer (Update), and the day only ends once nobody is left in the lab, so running sessions are safe
            return grade;
        }

        /// <summary>
        /// The research timer ran out (Client calls this when its data fires OnResearchFinished).
        /// The client waits for the player to collect the result. The researcher stays Busy until then.
        /// </summary>
        public void HandleResearchFinished(Client client)
        {
            if (!client || !sessions.TryGetValue(client.Data, out ResearchSession session)) return;
            
            Debug.Log($"{session.ResearcherData.Name} finished with {client.Data.Name}. Click the client to collect.");

            // DATA LOG: research_finished (researcher stops / post-research wait starts).
            // endedBy is "timer" normally, "day_end" when EndDay cut the session short
            ClientLog.ResearchFinished(client.Data, session.ResearcherData, endingDay ? "day_end" : skippingDay ? "debug_skip_day" : "timer");

            ResearchFinished?.Invoke(session);
        }
        
        /// <summary>True when this client's research is done and the player can collect it.</summary>
        public bool CanCollect(Client client)
        {
            return client && client.Data.IsReadyToCollect && sessions.ContainsKey(client.Data);
        }
        
        /// <summary>
        /// The player clicked a client whose research is done: pays the reward worked out at MATCH (0 if the match
        /// was wrong), saves {request ID : methodology ID}, frees the researcher, and the client leaves.
        /// </summary>
        public void CollectResearch(Client client)
        {
            if (!CanCollect(client)) return;

            ResearchSession session = sessions[client.Data];
            sessions.Remove(client.Data);
            
            GiveExperience(session, client.Data);
            RequestData request = client.Data.Request;

            if (request != null)
                RecordStudy(request.RequestId, session.Approach, session.MethodologyId, session.Reward);
            Debug.Log($"Collected {client.Data.Name}'s research: +${session.Reward}, {session.ResearcherData.Name} +{session.Experience} XP " +
                      $"(now {session.ResearcherData.Experience} XP, level {session.ResearcherData.Level}).");
            
            ClientLog.Reengaged(client.Data, endingDay || skippingDay);
            ClientLog.StudyCollected(client.Data, session.Grade, session.Reward);

            client.Data.Complete();
            UntrackClient(client.Data);   // same as CompleteStudy did, now at the moment the client leaves
            client.Leave();

            StudyFinished?.Invoke(session.Grade.Method);
            ClientResolved?.Invoke(new ClientResult
            {
                Request = request,
                Approach = session.Approach,
                ApproachCorrect = session.Grade.ApproachCorrect,
                MethodologyId = session.MethodologyId,
                Outcome = ClientResult.FromStudy(session.Grade.Method),
                Reward = session.Reward,
                ResearcherName = session.ResearcherData.Name,  
                ExperienceGained = session.Experience
            });

            // CLIENT ENTRANCE (changed): "no clients left" used to be checked here (SpawnNextClient). Update does it now
        }

        // RESEARCHER XP
        /// <summary>
        /// Frees the researcher (Busy -> Available) and gives them the XP worked out at MATCH, in one call:
        /// ResearcherData.FinishAssignment(xp) already did both, it just never got a number before.
        /// Then, if the XP pushed them past 2 / 5 / 10, logs and announces each new level.
        /// </summary>
        private void GiveExperience(ResearchSession session, ClientData client)
        {
            ResearcherData researcher = session.ResearcherData;
            int levelBefore = researcher.Level;

            researcher.FinishAssignment(session.Experience);

            ResearcherLog.ExperienceGained(researcher, client, session.Experience, levelBefore);   // DATA LOG: researcher_xp

            // a loop, not an if: with bigger XP numbers in the Inspector someone could jump from level 1 to 3 at once
            for (int level = levelBefore + 1; level <= researcher.Level; level++)
            {
                int unlocked = researcher.MethodologyUnlockedAt(level);
                Debug.Log($"{researcher.Name} reached level {level} and can now use {MethodLibrary.GetName(unlocked)}.");
                ResearcherLog.LeveledUp(researcher, level, unlocked);   // DATA LOG: researcher_level_up
                ResearcherLeveledUp?.Invoke(new ResearcherLevelUp
                {
                    Researcher = researcher,
                    NewLevel = level,
                    UnlockedMethodologyId = unlocked
                });
            }
        }

        /// <summary>Starts a session and finishes + collects it straight away (debug only).</summary>
        private void ResearchInstantly(Client client, Researcher researcher, ResearchApproach approach, int methodologyId)
        {
            StartResearch(client, researcher, approach, methodologyId);
            if (!client || !client.Data.IsInResearch) return;   // the researcher couldn't take them
            client.Data.FinishResearch();   // timer done (HandleResearchFinished)
            CollectResearch(client);         // frees the researcher for the next skipped client
        }

        // RESEARCH TIMER
        /// <summary>
        /// Called by EndDay. Any session still running is finished early, and every finished one is collected for
        /// the player, so the money is in today's summary and no researcher stays Busy into the next day.
        /// </summary>
        private void WrapUpResearchForDayEnd()
        {
            if (sessions.Count == 0) return;
            endingDay = true;
            try
            {
                // copy the list first: collecting removes from the dictionary, and C# doesn't allow that mid-loop
                foreach (KeyValuePair<ClientData, ResearchSession> entry in sessions.ToList())
                {
                    ResearchSession session = entry.Value;
                    if (!session.Client)   // the body was destroyed some other way. just free the researcher
                    {
                        // RESEARCHER XP (changed): was FinishAssignment(). The study was still done, so they still get the XP
                        GiveExperience(session, entry.Key);
                        sessions.Remove(entry.Key);
                        continue;
                    }
                    if (session.Client.Data.IsInResearch)
                        session.Client.Data.FinishResearch();   // fires OnResearchFinished -> HandleResearchFinished
                    CollectResearch(session.Client);
                }
            }
            finally
            {
                endingDay = false;
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

        // HIRE COST
        /// <summary>What hiring one researcher in the shop costs ($200 by default, "Hire Cost" in the Inspector).</summary>
        public int HireCost => hireCost;
        
        /// <summary>True if the player has at least HireCost. The hire tab greys out HIRE when they don't.</summary>
        public bool CanAffordHire => dataManager != null && dataManager.PlayerData.CurrentMoney >= hireCost;
        
        /// <summary>HIRE in the shop. Checks the money, puts the researcher in the first free cubicle, then takes HireCost.</summary>
        /// <returns>False (and nothing is paid) if the player can't afford it, there's no free cubicle, or they're already hired.</returns>
        public bool Hire(ResearcherData researcher)
        {
            if (researcher == null) return false;
            if (!CanAffordHire)
            {
                Debug.Log($"Hiring {researcher.Name} costs ${hireCost}, but there's only ${dataManager.PlayerData.CurrentMoney}.");
                return false;
            }

            // place first, pay second: if placing fails (no desk) nothing has been paid, so nothing needs refunding
            if (HireResearcher(researcher) == null) return false;

            // can't fail: CanAffordHire was checked above and nothing in between spends money.
            // TrySpend fires MoneySpent, so the HUD and the shop's money label update
            TrySpend(hireCost);
            Debug.Log($"Hired {researcher.Name} for ${hireCost} (${dataManager.PlayerData.CurrentMoney} left).");
            ResearcherLog.Hired(researcher, hireCost, dataManager.PlayerData.CurrentMoney);   // DATA LOG: researcher_hired
            return true;
        }

        // hires a researcher into the first free cubicle, for FREE. Hire (above) is the paid version for the shop.
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
        
        // ---------- SAVE GAME: starting a new game, or carrying on from the save ----------

        /// <summary>True when there's a saved game to carry on (the start screen shows CONTINUE).</summary>
        public bool HasSavedGame => dataManager != null && dataManager.HasSavedGame;

        /// <summary>
        /// START DAY 1 was pressed (StartScreen.GameStarted). Always a NEW game now. selectedStarter is the card the
        /// player picked (0 = first row of ResearcherGenerator's Starting Researchers list).
        /// SAVE GAME (changed): this used to also carry on a saved game when selectedStarter was -1. That's
        /// ContinueSavedGame (below) now. If there was a save, it's thrown away here (one save at a time).
        /// </summary>
        public void OnGameStarted(int selectedStarter)
        {
            // SAVE GAME (new): start from a clean slate, even if a save was loaded when the game opened.
            // A new seed too, so the new game doesn't get the same clients and researchers as the old one.
            // Without a save the seed from startup is kept (so GameInitializer's Override Seed still works)
            bool hadSave = dataManager.HasSavedGame;
            int seed = hadSave ? Environment.TickCount : dataManager.Seed;
            GameSeed.Set(seed);
            dataManager.StartNewGame(seed);
            if (hadSave) DeleteSaveInBackground();

            researcherSpawner.Clear();
            List<ResearcherData> pool = researcherGenerator.Generate();
            if (pool != null) dataManager.UpdateResearchers(pool);
            if (upgrades != null) upgrades.LoadSaveData(new Dictionary<string, int>());   // no upgrades in a new game

            ResearcherData starter = researcherGenerator.GetStarter(selectedStarter);
            if (starter == null)
                Debug.LogError($"FirmController: there's no starter number {selectedStarter}. Check ResearcherGenerator's Starting Researchers list.");
            else
                HireResearcher(starter);   // HIRE COST: the free version, the starter doesn't cost $200

            // FIRST DAY (new): day 1's clients depend on who was picked, so they're made here
            SetDailyClients(true, starter);

            // DATA LOG: game_start (new_game true)
            GameLog.GameStarted(true, starter, researcherGenerator.GetHiredResearchers());

            StartDay();
            SpawnNextClient();
        }

        // SAVE GAME (new)
        /// <summary>
        /// CONTINUE was pressed on the start screen. The save was loaded when the game opened, so this only puts it
        /// back in the lab: the hired researchers in their cubicles and the upgrades. It does NOT start a day.
        /// The save was made at a day summary, so UIInitializer carries on from there: the mid-game quiz if
        /// it's due, then NextDay (or the final quiz after the last day).
        /// </summary>
        public void ContinueSavedGame()
        {
            if (upgrades != null)
                upgrades.LoadSaveData(dataManager.PlayerData.OwnedUpgrades.ToDictionary(p => p.Key, p => p.Value));

            researcherSpawner.Clear();
            researcherSpawner.SpawnHired(researcherGenerator.GetHiredResearchers());

            // DATA LOG: game_start (new_game false)
            GameLog.GameStarted(false, null, researcherGenerator.GetHiredResearchers());
            Debug.Log($"FirmController: carrying on the saved game after day {CurrentDay}.");
        }

        // SAVE GAME (new)
        /// <summary>Last error from SaveGame, or null if the last save worked. The day summary shows it.</summary>
        public string LastSaveError { get; private set; }

        // SAVE GAME (new)
        /// <summary>
        /// Saves the game: money, day, answers, researchers (hired, XP, cubicle), upgrades and returning clients.
        /// The day summary calls this from SAVE &amp; CONTINUE and SAVE &amp; QUIT. Returns false if it failed
        /// (LastSaveError says why). This replaces SaveAfterDay, which was called by EndDay with the save commented out.
        /// </summary>
        public async Awaitable<bool> SaveGame()
        {
            int day = CurrentDay;
            try
            {
                // the upgrades live in the UpgradeController, so they're copied into the save first
                if (upgrades != null) dataManager.SetOwnedUpgrades(upgrades.ToSaveData());
                await dataManager.SaveGame();
                LastSaveError = null;
                DayLog.Saved(day, true, null);   // DATA LOG: game_saved
                return true;
            }
            catch (Exception e)
            {
                LastSaveError = e.Message;
                DayLog.Saved(day, false, e.Message);   // DATA LOG: game_saved (failed)
                Debug.LogError("FirmController: the game couldn't be saved.");
                Debug.LogException(e);
                return false;
            }
        }

        // SAVE GAME (new)
        /// <summary>True once the day 4 quiz is done, so carrying on from a save doesn't ask it twice.</summary>
        public bool MidGameQuizTaken => dataManager != null && dataManager.PlayerData.MidGameQuizTaken;

        // SAVE GAME (new)
        /// <summary>The mid-game quiz was finished. Remembered in the save (call SaveGame after).</summary>
        public void MarkMidGameQuizTaken() => dataManager.SetMidGameQuizTaken(true);

        // SAVE GAME (new)
        /// <summary>Deletes the save (the game is over, or a new game replaced it). Errors only go to the Console.</summary>
        public async void DeleteSaveInBackground()
        {
            try
            {
                await dataManager.DeleteSave();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"FirmController: couldn't delete the old save ({e.Message}).");
            }
        }

        // CLIENT ENTRANCE (changed)
        /// <summary>
        /// Sends the next client of the day in through the entrance, if the entrance is free.
        /// Update calls this every Seconds Between Clients. The demo "Spawn Client" button calls it too.
        /// It used to be called every time a client finished, and it ended the day when the list was empty:
        /// Update does that now (EndDayIfEveryoneIsDone).
        /// </summary>
        public void SpawnNextClient()
        {
            //Do not spawn a client if the day is over!
            if (dayRunning == false || skippingDay)
            {
                return;
            }
            if (clientGenerator == null || clientSpawner == null) return;
            if (dataManager.Clients.Count == 0) return;   // everyone for today has come in

            // CLIENT ENTRANCE (new): one at a time at the entrance
            if (EntranceTaken) return;

            var client = dataManager.Clients[0];
            dataManager.RemoveDailyClient(0);   // so the next call sends in the next client, not the same one again

            client.StartEntranceWait(entranceWaitSeconds);   // CLIENT ENTRANCE (new): the 15 seconds start now

            //Track the clients state changes
            Client spawned = clientSpawner.Spawn(client);
            ClientLog.Entered(client);   // DATA LOG: client_enter (wait starts)
            OnClientSpawned(spawned);

            atEntrance = spawned;   // CLIENT ENTRANCE (new)
            sinceLastEntry = 0f;
        }

        // CLIENT ENTRANCE (new)
        /// <summary>True while a client stands at the entrance waiting to be greeted.</summary>
        public bool EntranceTaken
        {
            get
            {
                // they were helped, sent somewhere, left, or their GameObject is gone: the entrance is free
                if (atEntrance && atEntrance.Data != null && atEntrance.Data.IsAtEntrance) return true;
                atEntrance = null;
                return false;
            }
        }

        // CLIENT ENTRANCE (new)
        /// <summary>
        /// Ends the day early when today's list is empty and nobody is left in the lab (waiting, following,
        /// with a researcher or waiting to be collected). Used to be SpawnNextClient's job.
        /// </summary>
        private void EndDayIfEveryoneIsDone()
        {
            if (clientSpawner == null || dataManager.Clients.Count > 0 || sessions.Count > 0) return;
            foreach (Client c in clientSpawner.ActiveClients)
                if (c) return;   // someone is still here
            EndDay(DayEndReason.NoClientsLeft);
        }

        // ---------- the day ----------

        private float timeLeft;
        private bool dayRunning;
        private bool skippingDay;

        /// <summary>Today's length: the base length plus the Clock upgrade. A Clock bought during the day counts from the next day.</summary>
        public float DayLength => baseDayLength + (upgrades != null ? upgrades.ExtraDaySeconds : 0f);

        /// <summary>Seconds left today. Before the first day starts it's the full length, after the day ends it's 0.</summary>
        public float TimeLeft => timeLeft;

        /// <summary>True while the timer is counting down.</summary>
        public bool IsDayRunning => dayRunning;

        /// <summary>The day being played, from the save, so it carries over between sessions.</summary>
        public int CurrentDay => dataManager != null ? dataManager.PlayerData.CurrentDay : 1;

        public int TotalDays => totalDays;

        public bool IsMidDay => CurrentDay == 4;
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
            {
                EndDay(DayEndReason.Timer);
                return;
            }

            // CLIENT ENTRANCE (new): the next client walks in every Seconds Between Clients, once the entrance is free
            sinceLastEntry += Time.deltaTime;
            if (sinceLastEntry >= secondsBetweenClients && !EntranceTaken)
                SpawnNextClient();

            EndDayIfEveryoneIsDone();   // CLIENT ENTRANCE (new)
        }

        /// <summary>Starts the timer for the current day. Called by OnGameStarted and NextDay.</summary>
        private void StartDay()
        {
            timeLeft = DayLength;
            dayRunning = true;
            atEntrance = null;                        // CLIENT ENTRANCE (new)
            sinceLastEntry = secondsBetweenClients;   // so the first client comes in straight away
            Debug.Log($"FirmController: day {CurrentDay} started ({DayLength:0}s).");
            DayLog.Started(CurrentDay, DayLength, dataManager.Clients != null ? dataManager.Clients.Count : 0);   // DATA LOG: day_start
            DayStarted?.Invoke(CurrentDay);
        }

        /// <summary>
        /// Ends today: stops the timer and tells everyone. Called when the timer runs out, and by
        /// EndDayIfEveryoneIsDone when there are no clients left. Does nothing if the day already ended,
        /// so both happening doesn't open the day summary twice.
        /// reason is only for the data log (DayEndReason.Timer, NoClientsLeft, or Failed once days can be failed).
        /// </summary>
        public void EndDay(string reason = "other")
        {
            if (!dayRunning) return;
            dayRunning = false;
            timeLeft = 0f;

            // RESEARCH TIMER (new): pay out any research still going before the clean-up below removes the clients,
            // and before the day_end log and the day summary
            WrapUpResearchForDayEnd();

            int day = CurrentDay;
            Debug.Log($"FirmController: day {day} ended.");

            //Clean up for clients that may remain in the queue or in the firm
            StopFollowing(following);
            following = null;
            atEntrance = null;   // CLIENT ENTRANCE (new)
            UntrackAllClients();
            trackedClients.Clear();
            clientSpawner.Clear();
            dataManager.ClearDailyClients();

            // DATA LOG: day_end. If a fail rule is added, call EndDay(DayEndReason.Failed): clients still in the lab
            // are then logged as interrupted by the failed day
            DayLog.Ended(day, reason, dataManager.PlayerData.CurrentMoney, dataManager.PlayerData.TotalMoneyEarned);
            DayEnded?.Invoke(day);
            // SAVE GAME (changed): SaveAfterDay() was called here (with the save itself commented out). The game is now
            // saved from the day summary's buttons, AFTER the shop, so what was bought in the shop is in the save too
        }

        private void UntrackAllClients()
        {
            foreach (var client in trackedClients.ToArray())
                UntrackClient(client);
        }
        /// <summary>
        /// Debug function to skip to the end of a day answering all clients 
        /// Leaves one returning client
        /// </summary>
        [ContextMenu("Debug/Skip Day")]
        public void SkipDay()
        {
            if (!Application.isPlaying || !dayRunning || skippingDay)
                return;

            // StartResearch needs a researcher GameObject.
            Researcher researcher = null;

            for (int cubicle = 1;
                 cubicle <= researcherSpawner.CubicleCount;
                 cubicle++)
            {
                researcher = researcherSpawner.GetAtCubicle(cubicle);
                if (researcher != null) break;
            }

            if (researcher == null)
            {
                Debug.LogWarning("SkipDay: hire a researcher first.");
                return;
            }

            skippingDay = true;

            try
            {
                // Include clients already in the lab.
                var clients = clientSpawner.ActiveClients.ToList();

                // Spawn the remaining queued clients so we can use
                // the existing gameplay methods on them.
                while (dataManager.Clients.Count > 0)
                {
                    var data = dataManager.Clients[0];
                    dataManager.RemoveDailyClient(0);

                    var client = clientSpawner.Spawn(data);
                    if (client == null) continue;

                    OnClientSpawned(client);
                    ClientLog.Entered(data);
                    clients.Add(client);
                }

                foreach (var client in clients)
                {
                    if (client == null) continue;

                    switch (UnityEngine.Random.Range(0, 4))
                    {
                        case 0:
                            SendHomeUntilTomorrow(client);
                            break;

                        case 1:
                            // Fires the existing dissatisfied-client handler.
                            client.Data.RunOutOfPatience();
                            break;

                        case 2:
                            var request = client.Data.Request;

                            if (request != null && request.IsResearchable)
                            {
                                //0 StartResearch refuses methods the researcher hasn't unlocked.
                                // Use the best one if they have it, otherwise their first method (so it still gets researched)
                                int method = researcher.Data.CanUseMethodology(request.BestMethodologyId)
                                    ? request.BestMethodologyId
                                    : researcher.Data.MethodologyUnlockedAt(1);

                                // RESEARCH TIMER (changed): was CompleteStudy. Skipping can't wait for the timer
                                ResearchInstantly(
                                    client,
                                    researcher,
                                    request.Approach,
                                    method);
                            }
                            else
                            {
                                // The proper answer to an unresearchable request.
                                RejectRequest(client);
                            }
                            break;

                        case 3:
                            RejectRequest(client);
                            break;
                    }
                }
                
                // Sets TimeLeft to zero and fires DayEnded.
                EndDay(DayEndReason.Timer);
            }
            finally
            {
                skippingDay = false;
            }
        }

        /// <summary>
        /// Moves to the next day: day number +1 (saved), a fresh set of clients, the timer starts and the
        /// first client comes in. DaySummary calls this when the player presses Continue.
        /// SAVE GAME: also how a saved game carries on (UIInitializer, after ContinueSavedGame).
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
            SetDailyClients(false);   // uses the new day number and skips requests already answered
            StartDay();
            SpawnNextClient();
        }


    }
}
