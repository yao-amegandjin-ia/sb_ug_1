package gameapi.repository;

import gameapi.entity.Client;
import gameapi.entity.ClientId;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.List;

/**
 * Repository access for client.
 */
public interface ClientRepository extends JpaRepository<Client, ClientId> {

    /** All RETURNING clients of a session, ordered by client ID. */
    @Query("select c from Client c where c.id.sessionId = :sid order by c.id.clientId")
    List<Client> findAllBySessionId(@Param("sid") String sessionId);

    /**
     * Deletes all clients of a session. Used in saving and at the end of the game.
     */
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Client c where c.id.sessionId = :sid")
    int deleteAllBySessionId(@Param("sid") String sessionId);

    /**
     * Cleanup: deletes the clients of every session last played before {@code cutoff}.
     * @return number of rows deleted
     */
    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Client c where c.id.sessionId in "
            + "(select s.sessionId from Session s where s.lastPlayed < :cutoff)")
    int deleteForSessionsPlayedBefore(@Param("cutoff") LocalDateTime cutoff);

}