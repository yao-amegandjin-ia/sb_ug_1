package gameapi.repository;

import gameapi.entity.Player;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.Optional;

public interface PlayerRepository extends JpaRepository<Player, String> {

    /**
     * Cleanup: deletes the player rows of every session last played before {@code cutoff}.
     *
     * @return number of rows deleted
     */
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Player p where p.sessionId in "
            + "(select s.sessionId from Session s where s.lastPlayed < :cutoff)")
    int deleteForSessionsPlayedBefore(@Param("cutoff") LocalDateTime cutoff);

}