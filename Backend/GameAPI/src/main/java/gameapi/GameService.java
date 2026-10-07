package gameapi;

import gameapi.DTOS.*;
import gameapi.entity.Client;
import gameapi.entity.Session;
import gameapi.entity.Player;
import gameapi.entity.Researcher;
import gameapi.repository.ClientRepository;
import gameapi.repository.SessionRepository;
import gameapi.repository.PlayerRepository;
import gameapi.repository.ResearcherRepository;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.*;

@Service
public class GameService {

    private final SessionRepository sessions;
    private final PlayerRepository players;
    private final ResearcherRepository researchers;
    private final ClientRepository clients;

    public GameService(SessionRepository sessions, PlayerRepository players,
                       ResearcherRepository researchers, ClientRepository clients) {
        this.sessions = sessions;
        this.players = players;
        this.researchers = researchers;
        this.clients = clients;
    }

    private static LocalDateTime now() {
        return LocalDateTime.now(ZoneOffset.UTC);
    }

    private Session findSession(String sessionId) {
        return sessions.findById(sessionId)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "Session not found"));
    }

    private static SessionResponse toResponse(Session s) {
        return new SessionResponse(s.getSessionId(), s.getPlayerUuid(), s.getLastPlayed());
    }

    /** Returns the players session by looking through player ID. If no session is found, make a new one. */
    @Transactional
    public SessionResponse createOrResume(String playerUuid) {
        Session s = sessions.findFirstByPlayerUuidOrderByLastPlayedDesc(playerUuid)
                .orElseGet(() -> new Session(playerUuid));
        s.setLastPlayed(now());
        return toResponse(sessions.save(s));
    }

    /** This is the response. We accept session ID, then find data from this ID to send to Unity*/
    @Transactional(readOnly = true)
    public SaveResponse load(String sessionId) {
        Session s = findSession(sessionId);
        Player p = players.findBySessionId(sessionId)
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "No save data yet for this session"));

        PlayerData pd = new PlayerData(p.getCurrentMoney(), p.getTotalMoneyEarned(), p.getCurrentDay(),
                p.getStudyResults(), p.getOwnedUpgrades());
        List<ResearcherData> rs = researchers.findAllBySessionId(sessionId).stream()
                .map(r -> new ResearcherData(r.getResearcherUuid(), r.getSkin(), r.getExperience(),
                        r.getMethodologies(), r.getSpeed(), r.getQuality(), r.getName()))
                .toList();
        List<ClientData> cs = clients.findAllBySessionId(sessionId).stream()
                .map(c -> new ClientData(c.getClientUuid(), c.getRequestId(),c.getSkin(),c.getName()))
                .toList();

        return new SaveResponse(s.getSessionId(), s.getPlayerUuid(), s.getLastPlayed(), pd, rs, cs);
    }

    /** This is the save. We accept session ID and request through this*/
    @Transactional
    public SessionResponse save(String sessionId, SaveRequest req) {
        Session s = findSession(sessionId);

        // Player (upsert)
        Player p = players.findById(s.getPlayerUuid()).orElseGet(Player::new);
        PlayerData pd = req.player();
        p.setPlayerUuid(s.getPlayerUuid());
        p.setSession(s);
        p.setCurrentMoney(pd.currentMoney());
        p.setTotalMoneyEarned(pd.totalMoneyEarned());
        p.setCurrentDay(pd.currentDay());
        p.setStudyResults(pd.studyResults() != null ? new HashMap<>(pd.studyResults()) : new HashMap<>());
        p.setOwnedUpgrades(pd.ownedUpgrades() != null ? new HashMap<>(pd.ownedUpgrades()) : new HashMap<>());
        players.save(p);

        //We delete researchers and clients from DB, then insert new ones in, as clients may have been solved and researchers may have been upgraded.
        researchers.deleteAllBySessionId(sessionId);
        clients.deleteAllBySessionId(sessionId);

        Set<String> seen = new HashSet<>();
        List<Researcher> newResearchers = new ArrayList<>();
        for (ResearcherData rd : Optional.ofNullable(req.researchers()).orElse(List.of())) {
            if (!seen.add(rd.researcherUuid()) || researchers.existsById(rd.researcherUuid())) {
                throw new ResponseStatusException(HttpStatus.CONFLICT, "Duplicate researcher_uuid: " + rd.researcherUuid());
            }
            Researcher r = new Researcher();
            r.setResearcherUuid(rd.researcherUuid());
            r.setSession(s);
            r.setSkin(rd.skin() != null ? new ArrayList<>(rd.skin()) : new ArrayList<>());
            r.setExperience(rd.experience());
            r.setMethodologies(rd.methodologies() != null ? new ArrayList<>(rd.methodologies()) : new ArrayList<>());
            r.setSpeed(rd.speed());
            r.setQuality(rd.quality());
            newResearchers.add(r);
        }
        researchers.saveAll(newResearchers);

        seen.clear();
        List<Client> newClients = new ArrayList<>();
        for (ClientData cd : Optional.ofNullable(req.clients()).orElse(List.of())) {
            if (!seen.add(cd.clientUuid()) || clients.existsById(cd.clientUuid())) {
                throw new ResponseStatusException(HttpStatus.CONFLICT, "Duplicate client_uuid: " + cd.clientUuid());
            }
            Client c = new Client();
            c.setClientUuid(cd.clientUuid());
            c.setSession(s);
            c.setRequestId(cd.requestId());
            newClients.add(c);
        }
        clients.saveAll(newClients);

        s.setLastPlayed(now());
        return toResponse(sessions.save(s));
    }

    //Sessions has delete cascade, so all other info will also be deleted
    @Transactional
    public void delete(String sessionId) {
        sessions.delete(findSession(sessionId));
    }

    //Sessions has delete cascade, so all other info will also be deleted.
    //We don't want old save files to be kept, so we will delete based on the days since last played
    @Transactional
    public int cleanup(int days) {
        return sessions.deleteOlderThan(now().minusDays(days));
    }
}