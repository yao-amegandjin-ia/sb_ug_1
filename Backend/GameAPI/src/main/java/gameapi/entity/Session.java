package gameapi.entity;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.UUID;


/**
 * This provides the Entity Session. Sessions are there to keep track of player saves/loads.
 * The purpose of having this separately is for maintainability.
 */
@Entity
@Table(name = "sessions", indexes = {
        @Index(name = "idx_sessions_player", columnList = "player_uuid"),
        @Index(name = "idx_sessions_last_played", columnList = "last_played")
})
@Getter @Setter @NoArgsConstructor
public class Session {

    @Id
    @Column(name = "session_id", length = 36)
    private String sessionId;

    // Plain column (not an FK) to avoid a circular FK with players.
    @Column(name = "player_uuid", length = 36, nullable = false)
    private String playerUuid;

    @Column(name = "last_played", nullable = false)
    private LocalDateTime lastPlayed;

    public Session(String playerUuid) {
        this.sessionId = UUID.randomUUID().toString();
        this.playerUuid = playerUuid;
        this.lastPlayed = LocalDateTime.now(ZoneOffset.UTC);
    }
}