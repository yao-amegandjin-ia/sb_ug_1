// FirmController - the game rules for what the player decides about clients and researchers.
// goes on the same GameObject as GameInitializer in the Bootstrap scene.
// the screens only show things and pass on clicks. every click that changes the game calls one function here,
// so the rules can be read (and changed) in one place without any UI code around them.
// money and study results live in the DataManager. the HUD and DaySummary hear what happened through the events below.
// it also owns the day: the timer, which day it is, starting the next one and ending this one.
// the HUD only shows TimeLeft / CurrentDay, and DaySummary opens on DayEnded and calls NextDay.
// RESEARCH TIMER: a match is no longer instant. MATCH starts a research session (StartResearch), the researcher
// and client are busy until the timer ends (HandleResearchFinished), then the player clicks the client to collect
// the money and they leave (CollectResearch). Search for "RESEARCH TIMER" to find every change.


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
        [Tooltip("How many of a researcher's methods can be used. The match screen's USS greys out buttons three and four, " +
                 "so this starts at 2. Set it to 4 to allow all of them.")]
        [SerializeField, Range(1, 4)] private int unlockedMethods = 2;

        // RESEARCH TIMER (new)
        [Header("Research sessions")]
        [Tooltip("Seconds a client spends with a researcher, before speed. Speed divides it: speed 2 = half as long.")]
        [SerializeField, Min(0f)] private float baseResearchSeconds = 20f;
        // RESEARCH TIMER (new, Nov 21): quality is no longer a straight multiplier
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

        // RESEARCH TIMER (new): nothing listens to these yet. They're here for a HUD / sound / animation later.
        /// <summary>MATCH was pressed and a research session started.</summary>
        public event Action<ResearchSession> ResearchStarted;
        /// <summary>A research session's timer ended. The researcher is free, the client waits to be collected.</summary>
        public event Action<ResearchSession> ResearchFinished;

        // RESEARCH TIMER (new): every client with a researcher or waiting to be collected, keyed by their data
        private readonly Dictionary<ClientData, ResearchSession> sessions = new();
        private bool endingDay;   // true while EndDay wraps up the sessions, so the log can say why they ended early
        private DataManager dataManager;
        //Clients from a save
        private readonly HashSet<ClientData> trackedClients = new();

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

            SetDailyClients(true);
            

            if (upgrades != null)
                upgrades.UpgradesChanged += ApplyClientUpgrades;

            // DATA LOG: every logged event gets the day number and the time left on the day timer
            GameLog.AttachDayClock(() => CurrentDay, () => timeLeft);
        }
        
        private void SetDailyClients(bool fromLoad)
        {

            var returningClients = dataManager.ReturningClients;
            var day = dataManager.PlayerData.CurrentDay;
            List<ClientData> clients;
            //Send in the usedrequestids, otherwise those are already known
            
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
            //Update the daily client list for the firm
            dataManager.UpdateDailyClients(clients, true);
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
            SpawnNextClient();
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
        public bool SendToWaitingRoom(Client client)
        {
            if (!client) return false;
            StopFollowing(client);

            if (!CanSendToWaitingRoom(client))
            {
                Debug.Log($"Waiting area is full ({WaitingCount}/{WaitingSeats}), so {client.Data.Name} gave up.");
                ClientLog.SentToWaiting(client.Data, false, WaitingCount, WaitingSeats);   // DATA LOG (client_leave follows)
                client.Data.RunOutOfPatience();   // Client hears this and calls HandleLeftDissatisfied below
                return false;
            }

            client.Data.SendToWaitingRoom();
            ClientLog.SentToWaiting(client.Data, true, WaitingCount, WaitingSeats);   // DATA LOG: client_sent_to_waiting
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
            ClientLog.Lost(client.Data);   // DATA LOG: client_leave (patience_ran_out / no_seat_in_waiting_room)

            StopFollowing(client);
            UntrackClient(client.Data);
            client.Leave();
            ClientLost?.Invoke(client);
            ClientResolved?.Invoke(new ClientResult { Request = request, MethodologyId = MethodLibrary.NoneId, Outcome = ClientOutcome.Lost });
            SpawnNextClient();
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
            SpawnNextClient();
        }

        
        // frees the "one client at a time" slot if this was the client following
        private void StopFollowing(Client client)
        {
            if (client == null || client != following) return;
            client.StopFollowing();
            following = null;
        }

        //Method Match Screen

        /// <summary>How many of this researcher's methods can be used. The same for everyone until levels exist.</summary>
        public int UnlockedMethodCount(ResearcherData researcher)
        {
            return unlockedMethods;
        }

        // RESEARCH TIMER (changed): this used to be CompleteStudy, which paid and sent the client away straight away.
        // Now MATCH only starts the session. The money is worked out here (so the grade is what the player picked)
        // but paid in CollectResearch, after the timer, when the player clicks the client.
        /// <summary>
        /// MATCH. Grades both steps (the approach, then the methodology), works out the money (matchRewards table,
        /// then the researcher's quality) and starts a research session. The researcher and the client are busy
        /// until the timer ends; the length is baseResearchSeconds shortened by the researcher's speed.
        /// </summary>
        public MatchGrade StartResearch(Client client, Researcher researcher, ResearchApproach approach, int methodologyId)
        {
            if (!client || !researcher) return new MatchGrade(false, StudyOutcome.Wrong);

            RequestData request = client.Data.Request;
            MatchGrade grade = StudyScoring.Grade(request, approach, methodologyId);   // methodology IDs come from the methodologies spreadsheet
            int baseReward = matchRewards.For(grade);

            // RESEARCH TIMER (new): speed -> how long, quality -> how much. See ResearchSession for the formulas
            ResearcherData researcherData = researcher.Data;
            float duration = ResearchSession.DurationFor(researcherData, baseResearchSeconds);
            float qualityMultiplier = ResearchSession.QualityMultiplier(researcherData, qualityBonusPerLevel);   // Nov 21: 1 / 1.25 / 1.5
            int reward = ResearchSession.RewardFor(baseReward, qualityMultiplier);

            // DATA LOG: the list of researchers who could have taken this request is worked out first, while
            // everyone's availability is still what the player saw (before this researcher becomes Busy)
            List<object> possibleResearchers = AnswerLog.FindCorrectResearchers(request, researcherGenerator.GetHiredResearchers(), unlockedMethods);

            // RESEARCH TIMER (new): the researcher is now Busy until the player collects the result (CollectResearch).
            // Researcher.OnPointerClick already ignores clicks on anyone who isn't IsAvailable,
            // so the player can't bring them a second client
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
                Duration = duration
            };
            sessions[client.Data] = session;

            Debug.Log($"{client.Data.Name} matched with {researcherData.Name}: {approach} / {MethodLibrary.GetName(methodologyId)} ({grade}). " +
                      $"Research takes {duration:0.#}s, pays ${reward} (base ${baseReward} x {qualityMultiplier:0.##} for quality {researcherData.Quality}).");

            // DATA LOG: match_answered (AnswerLog), then client_matched (ClientLog: greeting stops / researcher starts)
            AnswerLog.MatchAnswered(client.Data, researcherData, approach, methodologyId, grade, reward, possibleResearchers);
            ClientLog.Matched(client.Data, researcherData, grade, reward, duration);

            // RESEARCH TIMER (new): the client stops following and stays by the researcher.
            // StopFollowing also frees the "one client at a time" slot, so the player can take on the next client
            StopFollowing(client);
            client.Data.StartResearch(researcherData.Id, methodologyId, duration);   // state -> Assisted, the timer starts

            ResearchStarted?.Invoke(session);

            // RESEARCH TIMER (changed): CompleteStudy called SpawnNextClient here, because the client had left.
            // Now the next client still comes in at MATCH (so the player has someone to help during the timer),
            // but only if there is one. When the queue is empty, CollectResearch calls SpawnNextClient instead,
            // so the day doesn't end ("no clients left") while this session is still running.
            if (dataManager.Clients.Count > 0)
                SpawnNextClient();
            return grade;
        }

        // RESEARCH TIMER (new)
        /// <summary>
        /// The research timer ran out (Client calls this when its data fires OnResearchFinished).
        /// The client waits for the player to collect the result. The researcher stays Busy until then.
        /// </summary>
        public void HandleResearchFinished(Client client)
        {
            if (!client || !sessions.TryGetValue(client.Data, out ResearchSession session)) return;

            // RESEARCH TIMER (changed, Nov 21): the researcher used to be freed here. B wants them freed only when
            // the player collects, so FinishAssignment moved to CollectResearch
            Debug.Log($"{session.ResearcherData.Name} finished with {client.Data.Name}. Click the client to collect.");

            // DATA LOG: research_finished (researcher stops / post-research wait starts).
            // endedBy is "timer" normally, "day_end" when EndDay cut the session short
            ClientLog.ResearchFinished(client.Data, session.ResearcherData, endingDay ? "day_end" : skippingDay ? "debug_skip_day" : "timer");

            ResearchFinished?.Invoke(session);
        }

        // RESEARCH TIMER (new)
        /// <summary>True when this client's research is done and the player can collect it.</summary>
        public bool CanCollect(Client client)
        {
            return client && client.Data.IsReadyToCollect && sessions.ContainsKey(client.Data);
        }

        // RESEARCH TIMER (new): what CompleteStudy used to do after grading, moved to when the player collects.
        /// <summary>
        /// The player clicked a client whose research is done: pays the reward worked out at MATCH (0 if the match
        /// was wrong), saves {request ID : methodology ID}, frees the researcher, and the client leaves.
        /// </summary>
        public void CollectResearch(Client client)
        {
            if (!CanCollect(client)) return;

            ResearchSession session = sessions[client.Data];
            sessions.Remove(client.Data);

            // RESEARCH TIMER (changed, Nov 21): moved here from HandleResearchFinished.
            // The researcher only becomes Available (Busy -> Available) once the player has collected
            session.ResearcherData.FinishAssignment();
            RequestData request = client.Data.Request;

            if (request != null)
                RecordStudy(request.RequestId, session.Approach, session.MethodologyId, session.Reward);
            Debug.Log($"Collected {client.Data.Name}'s research: +${session.Reward}");

            // DATA LOG: client_reengaged (post-research wait stops / walkout starts), then client_leave.
            // autoCollected is true when the day ended and EndDay collected it for the player
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
                Reward = session.Reward
            });

            // RESEARCH TIMER: the queue was empty at MATCH, so this is where "no clients left" can end the day
            if (dataManager.Clients.Count == 0)
                SpawnNextClient();
        }

        // RESEARCH TIMER (new): used by SkipDay, which has no time to wait for the timer
        /// <summary>Starts a session and finishes + collects it straight away (debug only).</summary>
        private void ResearchInstantly(Client client, Researcher researcher, ResearchApproach approach, int methodologyId)
        {
            StartResearch(client, researcher, approach, methodologyId);
            if (!client || !client.Data.IsInResearch) return;   // the researcher couldn't take them
            client.Data.FinishResearch();   // timer done (HandleResearchFinished)
            CollectResearch(client);         // frees the researcher for the next skipped client
        }

        // RESEARCH TIMER (new)
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
                        session.ResearcherData.FinishAssignment();
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
                researcherSpawner.SpawnHired(hired);
            }

            // DATA LOG: game_start (selectedStarter is -1 when a saved game carries on)
            GameLog.GameStarted(selectedStarter != -1,
                selectedStarter != -1 ? researcherGenerator.GetById(9 + selectedStarter) : null,
                researcherGenerator.GetHiredResearchers());

            StartDay();
            SpawnNextClient();
        }

        /// <summary>
        /// This will need to be updated in the future.
        /// Currently, this is set up for the demo to spawn in the next client from the deck
        /// </summary>
        public void SpawnNextClient()
        {
            //Do not spawn a client if the day is over!
            if (dayRunning == false || skippingDay)
            {
                return;
            }
            if (clientGenerator == null || clientSpawner == null) return;
            //Reached the end of our daily clients!
            if (dataManager.Clients.Count == 0)
            {
                // RESEARCH TIMER (new): wait for running research sessions first. The last CollectResearch calls this again
                if (sessions.Count > 0) return;

                //Perform work for end of day
                EndDay(DayEndReason.NoClientsLeft);
                return;   // without this, clients[0] below throws on an empty list
            }

            var client = dataManager.Clients[0];
            dataManager.RemoveDailyClient(0);   // so the next call sends in the next client, not the same one again
            
            
            //Track the clients state changes
            Client spawned = clientSpawner.Spawn(client);
            ClientLog.Entered(client);   // DATA LOG: client_enter (wait starts)
            OnClientSpawned(spawned);
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
                EndDay(DayEndReason.Timer);
        }

        /// <summary>Starts the timer for the current day. Called by OnGameStarted and NextDay.</summary>
        private void StartDay()
        {
            timeLeft = DayLength;
            dayRunning = true;
            Debug.Log($"FirmController: day {CurrentDay} started ({DayLength:0}s).");
            DayLog.Started(CurrentDay, DayLength, dataManager.Clients != null ? dataManager.Clients.Count : 0);   // DATA LOG: day_start
            DayStarted?.Invoke(CurrentDay);
        }

        /// <summary>
        /// Ends today: stops the timer, saves and tells everyone. Called when the timer runs out, and by
        /// SpawnNextClient when there are no clients left. Does nothing if the day already ended,
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
            UntrackAllClients();
            trackedClients.Clear();
            clientSpawner.Clear();
            dataManager.ClearDailyClients();

            // DATA LOG: day_end. If a fail rule is added, call EndDay(DayEndReason.Failed): clients still in the lab
            // are then logged as interrupted by the failed day
            DayLog.Ended(day, reason, dataManager.PlayerData.CurrentMoney, dataManager.PlayerData.TotalMoneyEarned);
            DayEnded?.Invoke(day);
            SaveAfterDay();
        }

        private void UntrackAllClients()
        {
            foreach (var client in trackedClients.ToArray())
                UntrackClient(client);
        }
        // async void: Unity can't wait for this, so it runs alongside the game. fine for a save that nothing waits on
        private async void SaveAfterDay()
        {
            int day = CurrentDay;
            try
            {
                
                //await dataManager.SaveGame();
                DayLog.Saved(day, true, null);   // DATA LOG: game_saved
            }
            catch (Exception e)
            {
                DayLog.Saved(day, false, e.Message);   // DATA LOG: game_saved (failed)
                throw;   // still shows up in the Console like before
            }
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
                                // RESEARCH TIMER (changed): was CompleteStudy. Skipping can't wait for the timer
                                ResearchInstantly(
                                    client,
                                    researcher,
                                    request.Approach,
                                    request.BestMethodologyId);
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
