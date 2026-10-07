package gameapi.repository;

import gameapi.entity.Player;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.Optional;

public interface PlayerRepository extends JpaRepository<Player, String> {

    @Query("select p from Player p where p.session.sessionId = :sid")
    Optional<Player> findBySessionId(@Param("sid") String sessionId);
}