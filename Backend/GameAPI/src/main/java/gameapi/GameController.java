package gameapi;

import gameapi.DTOS.*;
import jakarta.validation.Valid;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;

import java.util.Map;

@RestController
public class GameController {

    private final GameService service;

    public GameController(GameService service) {
        this.service = service;
    }

    /**
     * This is to check if the server is up and running.
     * @return
     */
    @GetMapping("/health")
    public Map<String, String> health() {
        return Map.of("status", "ok");
    }

    /**
     * This is for creating or resuming a save file.
     * @param body We take in a session request
     * @return returns the saved session information
     */

    @PostMapping("/sessions")
    public SessionResponse createOrResume(@Valid @RequestBody CreateSessionRequest body) {
        return service.createOrResume(body.playerUuid());
    }

    /**
     * This loads in the save file data from the database
     * @param sessionId
     * @return Save file data
     */
    @GetMapping("/sessions/{sessionId}/save")
    public SaveResponse load(@PathVariable String sessionId) {
        return service.load(sessionId);
    }

    /**
     * This saves the session data to the database
     * @param sessionId
     * @param body Save data
     * @return
     */
    @PutMapping("/sessions/{sessionId}/save")
    public SessionResponse save(@PathVariable String sessionId, @Valid @RequestBody SaveRequest body) {
        return service.save(sessionId, body);
    }

    /**
     * Deletes a session by the session ID
     * @param sessionId
     */
    @DeleteMapping("/sessions/{sessionId}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void delete(@PathVariable String sessionId) {
        service.delete(sessionId);
    }

    /**
     * Deletes old game data after # of days
     * @param days Days since last played
     * @return # of deleted games
     */
    @PostMapping("/admin/cleanup")
    public Map<String, Integer> cleanup(@RequestParam int days) {
        if (days < 1) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "days must be >= 1");
        }
        return Map.of("deleted_sessions", service.cleanup(days));
    }
}
