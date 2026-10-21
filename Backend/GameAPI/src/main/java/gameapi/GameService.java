package gameapi;

import gameapi.DTOS.*;
import gameapi.entity.Client;
import gameapi.entity.ClientId;
import gameapi.entity.DataCollection;
import gameapi.entity.Player;
import gameapi.entity.Researcher;
import gameapi.entity.ResearcherId;
import gameapi.entity.Session;
import gameapi.repository.ClientRepository;
import gameapi.repository.DataCollectionRepository;
import gameapi.repository.PlayerRepository;
import gameapi.repository.ResearcherRepository;
import gameapi.repository.SessionRepository;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.*;
import java.util.concurrent.ThreadLocalRandom;

/**
 * All business logic of the save/load API. The controller only forwards requests here.
 *
 * <h3>Deleting data: no database cascade</h3>
 * None of the foreign keys use {@code ON DELETE CASCADE}. Rows are removed only by explicit calls in this class:
 * <ul>
 *   <li>{@link #deleteGameData} - researchers and clients of ONE session (the API call
 *       {@code DELETE /sessions/{id}/game-data}). {@link #end} and {@link #save} use the same repository calls.</li>
 *   <li>{@link #cleanup} - researchers, clients and players of every session not played for N days.</li>
 * </ul>
 * Because {@link DataCollection} (and everything else) has a real foreign key to {@link Session}, session rows are
 * never deleted. That is what keeps the research data attached to a valid session.
 *
 * <h3>What lives where</h3>
 * <ul>
 *   <li>KEPT after a game ends: {@link Session}, {@link Player}, {@link DataCollection}.</li>
 *   <li>DELETED when a game ends: {@link Researcher} and {@link Client}.</li>
 *   <li>DELETED by cleanup (stale sessions only): Researcher, Client, Player.</li>
 *   <li>NEVER deleted: Session, DataCollection.</li>
 * </ul>
 *
 * <h3>Persistence-context note</h3>
 * The delete queries on the researcher, client and player repositories clear Hibernate's first-level cache
 * ({@code clearAutomatically = true}), which detaches entities loaded earlier in the same transaction. That is
 * why this class re-fetches or uses {@code getReferenceById} after those calls, and why every value it still
 * needs is read BEFORE them.
 *
 * <p>Every public method is a single transaction: if anything fails, nothing is written.
 */
@Service
public class GameService {

    private final SessionRepository sessions;
    private final PlayerRepository players;
    private final ResearcherRepository researchers;
    private final ClientRepository clients;
    private final DataCollectionRepository dataCollections;

    public GameService(SessionRepository sessions, PlayerRepository players,
                       ResearcherRepository researchers, ClientRepository clients,
                       DataCollectionRepository dataCollections) {
        this.sessions = sessions;
        this.players = players;
        this.researchers = researchers;
        this.clients = clients;
        this.dataCollections = dataCollections;
    }

    // helpers

    /** Current UTC time. All timestamps in the database are UTC. */
    private static LocalDateTime now() {
        return LocalDateTime.now(ZoneOffset.UTC);
    }

    /** Loads a session or answers 404. */
    private Session findSession(String sessionId) {
        return sessions.findById(sessionId)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "Session not found"));
    }

    private static SessionResponse toResponse(Session s) {
        return new SessionResponse(s.getSessionId(), s.getPlayerUuid(), s.getSeed(), s.getLastPlayed(), s.getEndedAt());
    }

    private static DataCollectionResponse toResponse(DataCollection d) {
        return new DataCollectionResponse(d.getSessionId(), d.getPlayerUuid(), d.getStudyResults(), d.getScore(),
                d.getMidGameQuizResponses(), d.getPostGameQuizResponses());
    }

    /** Returns the session's data-collection row, or a new (not yet saved) empty one if it does not exist yet. */
    private DataCollection dataCollectionFor(Session s) {
        return dataCollections.findById(s.getSessionId()).orElseGet(() -> new DataCollection(s));
    }

    /**
     * Deletes the researchers and clients of one session
     * The single place where a session's lab data is removed; shared by delete-game-data and end-of-game.
     * Clears the persistence context, so callers must re-fetch entities they still need.
     */
    private DeleteGameDataResponse removeGameData(String sessionId) {
        int r = 0;
        int c = 0;
        if(researchers.findAllBySessionId(sessionId)!= null){
            r = researchers.deleteAllBySessionId(sessionId);
        }
        if(clients.findAllBySessionId(sessionId)!= null){
            c=clients.deleteAllBySessionId(sessionId);
        }
        return new DeleteGameDataResponse(r, c);
    }

    /**
     * Writes the player's progress and mirrors the permanent parts (study results, score) into
     * {@link DataCollection}. Used by save and by end-of-game so both always agree.
     */
    private void applyPlayer(Session s, PlayerData pd) {
        // The player's primary key is the session ID. For a new player we only set the session association;
        // Hibernate copies the ID from it (@MapsId).
        Player p = players.findById(s.getSessionId()).orElseGet(Player::new);
        p.setSession(s);
        p.setPlayerUuid(s.getPlayerUuid());
        p.setCurrentMoney(pd.currentMoney());
        p.setTotalMoneyEarned(pd.totalMoneyEarned());
        p.setCurrentDay(pd.currentDay());
        p.setStudyResults(pd.studyResults() != null ? new HashMap<>(pd.studyResults()) : new HashMap<>());
        p.setOwnedUpgrades(pd.ownedUpgrades() != null ? new HashMap<>(pd.ownedUpgrades()) : new HashMap<>());
        players.save(p);
        syncDataCollection(s, p);
    }

    /** Copies study results and score from the player into the permanent data-collection row. */
    private void syncDataCollection(Session s, Player p) {
        DataCollection d = dataCollectionFor(s);
        d.setStudyResults(new HashMap<>(p.getStudyResults()));
        d.setScore(p.getTotalMoneyEarned());
        dataCollections.save(d);
    }

    //Sessions

    /**
     * Starts a game or resumes one.
     *
     * <ul>
     *   <li>No player ID supplied: a new player ID is generated.</li>
     *   <li>Player ID supplied and that player has an IN-PROGRESS session: that session is returned
     *       (resume). The supplied seed is ignored because a game's seed must not change.</li>
     *   <li>Otherwise a new session is created with the supplied seed, or a random one.</li>
     * </ul>
     */
    @Transactional
    public SessionResponse createOrResume(CreateSessionRequest req) {
        String playerUuid = (req == null || req.playerUuid() == null || req.playerUuid().isBlank())
                ? UUID.randomUUID().toString()
                : req.playerUuid();

        Session s = sessions.findFirstByPlayerUuidAndEndedAtIsNullOrderByLastPlayedDesc(playerUuid).orElse(null);
        if (s == null) {
            int seed = (req != null && req.seed() != null)
                    ? req.seed()
                    : ThreadLocalRandom.current().nextInt(1, Integer.MAX_VALUE);
            s = new Session(playerUuid, seed);
        }
        s.setLastPlayed(now());
        s = sessions.save(s);
        dataCollections.save(dataCollectionFor(s)); // makes sure the research row exists from the start
        return toResponse(s);
    }

    /**
     * Loads everything needed to rebuild a game: seed, player progress, researchers and clients.
     * For a finished game the researcher and client lists are empty because they were deleted at the end.
     */
    @Transactional(readOnly = true)
    public SaveResponse load(String sessionId) {
        Session s = findSession(sessionId);
        Player p = players.findById(sessionId)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "No save data yet for this session"));

        PlayerData pd = new PlayerData(p.getCurrentMoney(), p.getTotalMoneyEarned(), p.getCurrentDay(),
                p.getStudyResults(), p.getOwnedUpgrades());

        List<ResearcherData> rs = researchers.findAllBySessionId(sessionId).stream()
                .map(r -> new ResearcherData(r.getId().getResearcherId(), r.getName(), r.getSkin(), r.getExperience(),
                        r.getMethodologies(), r.getSpeed(), r.getQuality(), r.getPosition()))
                .toList();

        List<ClientData> cs = clients.findAllBySessionId(sessionId).stream()
                .map(c -> new ClientData(c.getId().getClientId(), c.getRequestId(), c.getSkin()))
                .toList();

        return new SaveResponse(s.getSessionId(), s.getPlayerUuid(), s.getSeed(), s.getLastPlayed(), pd, rs, cs);
    }

    /**
     * Saves the game. The player row is updated in place; researchers and clients are REPLACED (deleted and
     * re-inserted) because clients get solved and researchers change between saves.
     *
     * <p>All validation happens before anything is written, so a rejected save leaves the old save untouched.
     *
     * @throws ResponseStatusException 404 unknown session; 409 game already ended; 400 duplicate researcher ID,
     *                                 duplicate client ID, or two researchers in the same cubicle
     */
    @Transactional
    public SessionResponse save(String sessionId, SaveRequest req) {
        Session s = findSession(sessionId);
        if (s.getEndedAt() != null) {
            throw new ResponseStatusException(HttpStatus.CONFLICT, "This game has ended; start a new session");
        }

        List<ResearcherData> newR = Optional.ofNullable(req.researchers()).orElse(List.of());
        List<ClientData> newC = Optional.ofNullable(req.clients()).orElse(List.of());

        // ---- validate first
        Set<Integer> seenIds = new HashSet<>();
        Set<Integer> seenPositions = new HashSet<>();
        for (ResearcherData rd : newR) {
            if (!seenIds.add(rd.researcherId())) {
                throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Duplicate researcher_id: " + rd.researcherId());
            }
            int pos = rd.position() == null ? 0 : rd.position();
            if (pos > 0 && !seenPositions.add(pos)) {
                throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Two researchers share cubicle position " + pos);
            }
        }
        seenIds.clear();
        for (ClientData cd : newC) {
            if (!seenIds.add(cd.clientId())) {
                throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Duplicate client_id: " + cd.clientId());
            }
        }

        // ---- write
        s.setLastPlayed(now());
        s = sessions.save(s);
        applyPlayer(s, req.player());

        SessionResponse response = toResponse(s); // read now; the deletes below detach `s`

        removeGameData(sessionId);

        Session ref = sessions.getReferenceById(sessionId); // fresh reference after the cache was cleared

        List<Researcher> rows = new ArrayList<>();
        for (ResearcherData rd : newR) {
            Researcher r = new Researcher();
            r.setId(new ResearcherId(sessionId, rd.researcherId()));
            r.setSession(ref);
            r.setName(rd.name());
            r.setSkin(rd.skin() != null ? new ArrayList<>(rd.skin()) : new ArrayList<>());
            r.setExperience(rd.experience());
            r.setMethodologies(rd.methodologies() != null ? new ArrayList<>(rd.methodologies()) : new ArrayList<>());
            r.setSpeed(rd.speed());
            r.setQuality(rd.quality());
            r.setPosition(rd.position() == null ? 0 : rd.position());
            rows.add(r);
        }
        researchers.saveAll(rows);

        List<Client> clientRows = new ArrayList<>();
        for (ClientData cd : newC) {
            Client c = new Client();
            c.setId(new ClientId(sessionId, cd.clientId()));
            c.setSession(ref);
            c.setRequestId(cd.requestId());
            c.setSkin(cd.skin() != null ? new ArrayList<>(cd.skin()) : new ArrayList<>());
            clientRows.add(c);
        }
        clients.saveAll(clientRows);

        return response;
    }

    /**
     * Ends a game.
     *
     *
     *   If a final player state is supplied it is saved; the permanent study results and score are updated.
     *   All researchers and clients of the session are deleted (same call as {@link #deleteGameData})
     *   The session is marked ended. Session, Player and DataCollection remain.
     *
     * Calling it again on an ended session does nothing and returns the session (safe to retry).
     */
    @Transactional
    public SessionResponse end(String sessionId, EndGameRequest req) {
        Session s = findSession(sessionId);
        if (s.getEndedAt() != null) {
            return toResponse(s);
        }

        if (req != null && req.player() != null) {
            applyPlayer(s, req.player());
        } else {
            // No final state supplied: make sure the research row reflects the last saved player state.
            final Session session = s;
            players.findById(sessionId).ifPresent(p -> syncDataCollection(session, p));
        }

        removeGameData(sessionId);

        Session fresh = findSession(sessionId); // re-fetch: the deletes above detached the earlier instance
        fresh.setEndedAt(now());
        fresh.setLastPlayed(now());
        return toResponse(sessions.save(fresh));
    }

    // Data collection

    /**
     * Stores quiz answers for a session. Answers are MERGED into any already stored (same question ID is
     * overwritten). Allowed at any time, including after the game has ended, because the post-game quiz
     * happens after the end.
     *
     * @param midGame true for the mid-game quiz, false for the post-game quiz
     */
    @Transactional
    public DataCollectionResponse recordQuiz(String sessionId, Map<Integer, Integer> responses, boolean midGame) {
        Session s = findSession(sessionId);
        DataCollection d = dataCollectionFor(s);
        Map<Integer, Integer> target = midGame ? d.getMidGameQuizResponses() : d.getPostGameQuizResponses();
        Map<Integer, Integer> merged = new HashMap<>(target != null ? target : Map.of());
        merged.putAll(responses);
        if (midGame) {
            d.setMidGameQuizResponses(merged);
        } else {
            d.setPostGameQuizResponses(merged);
        }
        return toResponse(dataCollections.save(d));
    }

    /** Reads the permanent research record of a session. */
    @Transactional(readOnly = true)
    public DataCollectionResponse getDataCollection(String sessionId) {
        return dataCollections.findById(sessionId)
                .map(GameService::toResponse)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "No data collection for this session"));
    }

    // Deletion

    /**
     * Deletes all researchers and all clients of ONE session. This is the explicit replacement for a database
     * cascade. The session, player and data-collection rows are not touched.
     *
     * <p>Can be called at any time, also on a game in progress (the saved player stays, the lab is emptied; the
     * next save writes the lab again). Calling it twice is harmless: the second call deletes 0 rows.
     *
     * @return how many researchers and clients were deleted
     * @throws ResponseStatusException 404 if the session does not exist
     */
    @Transactional
    public DeleteGameDataResponse deleteGameData(String sessionId) {
        findSession(sessionId);
        return removeGameData(sessionId);
    }

    /**
     * Housekeeping for stale games: for every session last played before {@code now - days}, deletes its
     * researchers, clients and player, and marks it ended if it was still running (so it can not be resumed
     * without its save data). Sessions and DataCollection are kept.
     *
     * <p>Sessions that were already cleaned are matched again but have nothing left to delete, so running this
     * repeatedly is safe.
     */
    @Transactional
    public CleanupResponse cleanup(int days) {
        LocalDateTime cutoff = now().minusDays(days);
        int r = researchers.deleteForSessionsPlayedBefore(cutoff);
        int c = clients.deleteForSessionsPlayedBefore(cutoff);
        int p = players.deleteForSessionsPlayedBefore(cutoff);
        int closed = sessions.closeAbandonedBefore(cutoff, now());
        return new CleanupResponse(r, c, p, closed);
    }
}