package gameapi.repository;

import gameapi.entity.Researcher;
import gameapi.entity.ResearcherId;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.List;

/**
 * DB access for reserachers
 */
public interface ResearcherRepository extends JpaRepository<Researcher, ResearcherId> {

    /** All researchers of a session, ordered by researcher ID. */
    @Query("select r from Researcher r where r.id.sessionId = :sid order by r.id.researcherId")
    List<Researcher> findAllBySessionId(@Param("sid") String sessionId);

    /**
     * Deletes every researcher of a session. Used by the delete-game-data call, by save (replace-all) and by
     * end-of-game.
     *
     * @return number of rows deleted
     */
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Researcher r where r.id.sessionId = :sid")
    int deleteAllBySessionId(@Param("sid") String sessionId);

    /**
     * Cleanup: deletes the researchers of every session last played before cutoff
     *
     * @return number of rows deleted
     */
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Researcher r where r.id.sessionId in "
            + "(select s.sessionId from Session s where s.lastPlayed < :cutoff)")
    int deleteForSessionsPlayedBefore(@Param("cutoff") LocalDateTime cutoff);
}