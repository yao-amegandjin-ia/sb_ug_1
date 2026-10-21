package gameapi.entity;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.util.UUID;



/**
 * Session entity. This is the parent of other schemas and represents a game state.
 * A session ID is created at the start of the game.
 * lastPlayed is tracked in case we need to delete old save data
 * endedAt is used to track when a game ended to delete old data.
 */
@Entity
@Table(name = "sessions", indexes = {
        @Index(name = "idx_sessions_player", columnList = "player_uuid"),
        @Index(name = "idx_sessions_last_played", columnList = "last_played")
})
@Getter @Setter @NoArgsConstructor
public class Session {

    /** Primary key. Random UUID string */
    @Id
    @Column(name = "session_id", length = 36)
    private String sessionId;

    /** Plain column (NOT an FK). This is so one player can own many sessions (if needed/wanted) */
    @Column(name = "player_uuid", length = 36, nullable = false)
    private String playerUuid;

    /** UTC time of last activity. Used to delete stale sessions. */
    @Column(name = "last_played", nullable = false)
    private LocalDateTime lastPlayed;

    /** Seed for the game's random generators */
    @Column(name = "seed", nullable = false)
    private int seed;

    /** null = game in progress; non-null = game finished (researchers and clients already deleted). */
    @Column(name = "ended_at")
    private LocalDateTime endedAt;

    /**
     * Creates a brand-new, in-progress session.
     *
     * @param playerUuid the owning player
     * @param seed       the seed for this playthrough
     */
    public Session(String playerUuid, int seed) {
        this.sessionId = UUID.randomUUID().toString();
        this.playerUuid = playerUuid;
        this.seed = seed;
        this.lastPlayed = LocalDateTime.now(ZoneOffset.UTC);
    }
}