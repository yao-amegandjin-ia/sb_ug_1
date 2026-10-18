package gameapi;

import gameapi.DTOS.*;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.Map;

/**
 * REST endpoints for the Research Frenzy save/load API. This class only maps HTTP to
 * {@link GameService}; all logic is in the service. Interactive documentation: {@code /swagger-ui.html}.
 *
 * <pre>
 *   GET    /health
 *   POST   /sessions                          start or resume a game
 *   GET    /sessions/{id}/save                load
 *   PUT    /sessions/{id}/save                save
 *   POST   /sessions/{id}/end                 end the game (deletes researchers and clients)
 *   DELETE /sessions/{id}/game-data           delete a session's researchers and clients
 *   PUT    /sessions/{id}/quizzes/mid-game    store mid-game quiz answers
 *   PUT    /sessions/{id}/quizzes/post-game   store post-game quiz answers
 *   GET    /sessions/{id}/data-collection     read the permanent research record
 *   POST   /admin/cleanup?days=N              clear researchers, clients and players of stale sessions
 * </pre>
 *
 * There is no endpoint that deletes a session: sessions are referenced by the permanent data-collection table.
 *
 * Errors: 400 invalid body, 404 unknown session, 409 saving to a game that has already ended.
 */
@RestController
public class GameController {

    private final GameService service;

    public GameController(GameService service) {
        this.service = service;
    }

    /** Checks that the server is up. Does not touch the database. */
    @GetMapping("/health")
    public Map<String, String> health() {
        return Map.of("status", "ok");
    }

    /**
     * Starts a new game or resumes the player's in-progress game.
     *
     * @param body optional; may contain {@code player_uuid} and {@code seed}. A missing body, or {@code {}},
     *             creates a new player ID and a random seed.
     * @return the session, including its seed and the player UUID to store on the device
     */
    @PostMapping("/sessions")
    public SessionResponse createOrResume(@Valid @RequestBody(required = false) CreateSessionRequest body) {
        return service.createOrResume(body);
    }

    /**
     * Loads a saved game: seed, player progress, researchers and clients.
     *
     * @return 404 if the session is unknown or has never been saved
     */
    @GetMapping("/sessions/{sessionId}/save")
    public SaveResponse load(@PathVariable String sessionId) {
        return service.load(sessionId);
    }

    /**
     * Saves the game. Replaces the stored researchers and clients with the lists in the body.
     *
     * @return 409 if the game has ended; 400 for duplicate IDs or shared cubicle positions
     */
    @PutMapping("/sessions/{sessionId}/save")
    public SessionResponse save(@PathVariable String sessionId, @Valid @RequestBody SaveRequest body) {
        return service.save(sessionId, body);
    }

    /**
     * Ends the game. Researchers and clients are deleted; session, player and data-collection rows stay.
     * The body is optional and may carry the final player state. Safe to call twice.
     */
    @PostMapping("/sessions/{sessionId}/end")
    public SessionResponse end(@PathVariable String sessionId,
                               @Valid @RequestBody(required = false) EndGameRequest body) {
        return service.end(sessionId, body);
    }

    /**
     * Deletes all researchers and clients that belong to this session ID. Replaces the old database cascade.
     * Session, player and data collection are untouched. Safe to call twice (the second call deletes 0 rows).
     *
     * @return the number of researchers and clients deleted; 404 if the session does not exist
     */
    @DeleteMapping("/sessions/{sessionId}/game-data")
    public DeleteGameDataResponse deleteGameData(@PathVariable String sessionId) {
        return service.deleteGameData(sessionId);
    }

    /** Stores (merges) mid-game quiz answers {questionId : response}. */
    @PutMapping("/sessions/{sessionId}/quizzes/mid-game")
    public DataCollectionResponse midGameQuiz(@PathVariable String sessionId, @Valid @RequestBody QuizRequest body) {
        return service.recordQuiz(sessionId, body.responses(), true);
    }

    /** Stores (merges) post-game quiz answers {questionId : response}. Allowed after the game has ended. */
    @PutMapping("/sessions/{sessionId}/quizzes/post-game")
    public DataCollectionResponse postGameQuiz(@PathVariable String sessionId, @Valid @RequestBody QuizRequest body) {
        return service.recordQuiz(sessionId, body.responses(), false);
    }

    /** Reads the permanent research record (study results, score, quiz answers). */
    @GetMapping("/sessions/{sessionId}/data-collection")
    public DataCollectionResponse dataCollection(@PathVariable String sessionId) {
        return service.getDataCollection(sessionId);
    }

    /**
     * Clears old games: for every session not played for {@code days} days, deletes its researchers, clients and
     * player and marks it ended if it was still running. Sessions and data collection are never deleted.
     * NOTE: there is no authentication; protect this endpoint before exposing the server publicly.
     *
     * @param days at least 1
     * @return the number of rows removed per table, and how many running sessions were closed
     */
    @PostMapping("/admin/cleanup")
    public CleanupResponse cleanup(@RequestParam int days) {
        if (days < 1) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "days must be >= 1");
        }
        return service.cleanup(days);
    }
}