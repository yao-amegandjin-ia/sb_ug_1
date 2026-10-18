package gameapi.repository;

import gameapi.entity.Session;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.Optional;

/**
 * DB access for sessions
 *
 */
public interface SessionRepository extends JpaRepository<Session, String> {

    /**
     * Finds the player's most recently played session that is still IN PROGRESS (ended_at is null).
     * Finished games are skipped on purpose, so starting a new game never resumes an old, finished one.
     * Backed by the {@code idx_sessions_player} index.
     */
    Optional<Session> findFirstByPlayerUuidAndEndedAtIsNullOrderByLastPlayedDesc(String playerUuid);

    /**
     * Cleanup: marks every still-running session last played before {@code cutoff} as ended (abandoned), so a
     * session whose save data was just removed can never be resumed. {@code last_played} is not changed, so
     * running the cleanup again does not pick these up as new.
     *
     * @return number of sessions closed
     */
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("update Session s set s.endedAt = :now where s.lastPlayed < :cutoff and s.endedAt is null")
    int closeAbandonedBefore(@Param("cutoff") LocalDateTime cutoff, @Param("now") LocalDateTime now);
}