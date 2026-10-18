package gameapi;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.time.LocalDateTime;
import java.util.List;
import java.util.Map;


/**
 * DTOs (Data trasnfer objects) helps with the JSON requests
 * We send and recieve everything in one call, instead of having to make multiple calls.
 *
 * Data Flow:
 * Unity JSON -> SaveRequest  -> GameService -> entities -> database
 * database   -> entities     -> GameService -> SaveResponse -> Unity JSON
 *
 * JSON property names are SNAKE CASE. totalMoneyEarned -> total_money_earned. Object keys are strings.
 * Map<Integer,Integer> -> {"1":"2"}
 */
public final class DTOS {
    private DTOS() {}

    // Sessions

    /**
     * Body of POST /sessions.
     *
     * @param playerUuid the player's ID. If omitted the backend generates one and returns it - store it on the
     *                   device so the same player can resume later. (Later this comes from the Cyclones API.)
     * @param seed       the seed for this game. Ignored when an
     *                   in-progress session is resumed, because a game's seed must never change.
     */
    public record CreateSessionRequest(@Size(max = 36) String playerUuid, Integer seed) {}

    /**
     * Basic session information, returned by create/resume, save and end.
     *
     * @param endedAt null while the game is in progress; the end time once it has finished.
     */
    public record SessionResponse(String sessionId, String playerUuid, int seed,
                                  LocalDateTime lastPlayed, LocalDateTime endedAt) {}

    // SAVE DATA

    /**
     * The player's progress.
     *
     * @param totalMoneyEarned the score
     * @param studyResults     {requestId : methodologyId} - answered requests and the player's chosen methodology
     * @param ownedUpgrades    {upgradeName : level}
     */
    public record PlayerData(
            int currentMoney,
            int totalMoneyEarned,
            int currentDay,
            Map<Integer, Integer> studyResults,
            Map<String, Integer> ownedUpgrades) {}

    /**
     * One researcher.
     *
     * @param researcherId   number assigned by the game; must be unique within the session
     * @param skin           appearance part IDs in order, for example [head, torso, legs]
     * @param methodologies  methodology IDs; the array index is the level the methodology unlocks at
     * @param position       cubicle number; 0 or null means not purchased
     */
    public record ResearcherData(
            @Min(0) int researcherId,
            @NotBlank @Size(max = 100) String name,
            List<Integer> skin,
            int experience,
            List<Integer> methodologies,
            int speed,
            int quality,
            @Min(0) Integer position) {}

    /**
     * One client in the lab.
     *
     * @param clientId  number assigned by the game; must be unique within the session
     * @param requestId the request this client brought
     * @param skin      appearance part IDs in order
     */
    public record ClientData(
            @Min(0) int clientId,
            int requestId,
            List<Integer> skin) {}

    /**
     * Body of {@code PUT /sessions/{id}/save}. This REPLACES the stored researchers and clients for the
     * session, so always send the complete current lists.
     */
    public record SaveRequest(
            @NotNull @Valid PlayerData player,
            @Valid List<ResearcherData> researchers,
            @Valid List<ClientData> clients) {}

    /** Response of {@code GET /sessions/{id}/save}: everything needed to rebuild the game. Includes the seed. */
    public record SaveResponse(
            String sessionId,
            String playerUuid,
            int seed,
            LocalDateTime lastPlayed,
            PlayerData player,
            List<ResearcherData> researchers,
            List<ClientData> clients) {}

    // End of game

    /**
     * Optional body of {@code POST /sessions/{id}/end}.
     *
     * @param player the final player state (final score and study results). If omitted, the last saved
     *               player state is used.
     */
    public record EndGameRequest(@Valid PlayerData player) {}

    // Deleting data

    /**
     * Response of {@code DELETE /sessions/{id}/game-data}: how many rows were removed. Both numbers are 0 when
     * the session had no researchers or clients (calling it twice is harmless).
     */
    public record DeleteGameDataResponse(int deletedResearchers, int deletedClients) {}

    /**
     * Response of {@code POST /admin/cleanup}.
     *
     * @param deletedResearchers researcher rows removed from stale sessions
     * @param deletedClients     client rows removed from stale sessions
     * @param deletedPlayers     player rows removed from stale sessions
     * @param closedSessions     stale sessions that were still marked in-progress and are now marked ended
     */
    public record CleanupResponse(int deletedResearchers, int deletedClients, int deletedPlayers, int closedSessions) {}

    // Data collection

    /**
     * Body of the quiz endpoints.
     *
     * @param responses {questionId : response}. Merged into any answers already stored for that quiz, so they
     *                  can be sent all at once or one at a time.
     */
    public record QuizRequest(@NotNull Map<Integer, Integer> responses) {}

    /** The permanent research record for a session. */
    public record DataCollectionResponse(
            String sessionId,
            String playerUuid,
            Map<Integer, Integer> studyResults,
            int score,
            Map<Integer, Integer> midGameQuizResponses,
            Map<Integer, Integer> postGameQuizResponses) {}
}