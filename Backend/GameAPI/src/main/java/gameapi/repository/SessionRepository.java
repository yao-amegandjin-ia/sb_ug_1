package gameapi.repository;

import gameapi.entity.Session;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.Optional;

public interface SessionRepository extends JpaRepository<Session, String> {

    //no query should be needed here, since it is indexed this way
    Optional<Session> findFirstByPlayerUuidOrderByLastPlayedDesc(String playerUuid);

    //Child rows (player/researchers/clients) are removed by the database's ON DELETE CASCADE.
    @Modifying
    @Query("delete from Session s where s.lastPlayed < :cutoff")
    int deleteOlderThan(@Param("cutoff") LocalDateTime cutoff);
}
