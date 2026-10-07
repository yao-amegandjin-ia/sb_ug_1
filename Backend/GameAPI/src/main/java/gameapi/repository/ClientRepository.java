package gameapi.repository;

import gameapi.entity.Client;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

public interface ClientRepository extends JpaRepository<Client, String> {

    @Query("select c from Client c where c.session.sessionId = :sid")
    List<Client> findAllBySessionId(@Param("sid") String sessionId);

    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Client c where c.session.sessionId = :sid")
    void deleteAllBySessionId(@Param("sid") String sessionId);
}
