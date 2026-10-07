package gameapi;

import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;

import java.time.LocalDateTime;
import java.util.List;
import java.util.Map;

/** Request/response shapes. JSON is snake_case (see application.properties). */

/**
 * The purpose of DTOS (data transfer objects) is to make it easy for transfering the json data
 * from the DB to the Unity Game. This way, we only need to send one file back and forth, rather than
 * having multiple API calls, which are unnecessary for the way this is being used
 *
 * Data flow:
 * Unity JSON -> Save Request - > Service -> Entities -> Database
 * DB -> entities -> service -> Save Response -> Unity JSON
 */
public final class DTOS {
    private DTOS() {}

    public record CreateSessionRequest(@NotBlank @Size(max = 36) String playerUuid) {}

    public record SessionResponse(String sessionId, String playerUuid, LocalDateTime lastPlayed) {}

    public record PlayerData(
            int currentMoney,
            int totalMoneyEarned,
            int currentDay,
            Map<Integer, Integer> studyResults, //{requestId:methodologyId}
            Map<String, Integer> ownedUpgrades) {} //{upgrade:level}

    public record ResearcherData(
            @NotBlank @Size(max = 36) String researcherUuid,
            List<Integer> skin,
            int experience,
            List<Integer> methodologies,
            int speed,
            int quality,
            String name) {}

    public record ClientData(
            @NotBlank @Size(max = 36) String clientUuid,
            int requestId,
            List<Integer> skin,
            String name) {}

    public record SaveRequest(
            @NotNull @Valid PlayerData player,
            @Valid List<ResearcherData> researchers,
            @Valid List<ClientData> clients) {}

    public record SaveResponse(
            String sessionId,
            String playerUuid,
            LocalDateTime lastPlayed,
            PlayerData player,
            List<ResearcherData> researchers,
            List<ClientData> clients) {}
}
