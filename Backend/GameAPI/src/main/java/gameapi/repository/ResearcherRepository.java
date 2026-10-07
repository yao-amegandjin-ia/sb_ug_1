package gameapi.repository;

import gameapi.entity.Researcher;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Modifying;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.util.List;

public interface ResearcherRepository extends JpaRepository<Researcher, String> {

    @Query("select r from Researcher r where r.session.sessionId = :sid")
    List<Researcher> findAllBySessionId(@Param("sid") String sessionId);

    @Modifying(flushAutomatically = true, clearAutomatically = true)
    @Query("delete from Researcher r where r.session.sessionId = :sid")
    void deleteAllBySessionId(@Param("sid") String sessionId);
}