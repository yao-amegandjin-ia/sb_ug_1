package gameapi.entity;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import java.util.HashMap;
import java.util.Map;

/**
 * This stores the data collected from Research Frenzy!
 * This information should NOT be deleted, as it will be used for research.
 */
@Entity
@Table(name = "data_collection", indexes = {
        @Index(name = "idx_data_collection_player", columnList = "player_uuid"),
        @Index(name = "idx_data_collection_score", columnList = "score")
})
@Getter @Setter @NoArgsConstructor
public class DataCollection {

    /** Primary key = foreign key to sessions. Filled in by Hibernate via {@link MapsId}; do not set manually. */
    @Id
    @Column(name = "session_id", length = 36)
    private String sessionId;

    /**
     * The owning session. Lazy because reads only need this row's own columns.
     */
    @MapsId
    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id")
    private Session session;

    @Column(name = "player_uuid", length = 36, nullable = false)
    private String playerUuid;

    /** {requestId : methodologyId} */
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "study_results")
    private Map<Integer, Integer> studyResults = new HashMap<>();

    /** Final (or latest) score = total money earned. Used for leaderboards. */
    @Column(name = "score", nullable = false)
    private int score;

    /** {questionId : response} */
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "mid_game_quiz_responses")
    private Map<Integer, Integer> midGameQuizResponses = new HashMap<>();

    /** {questionId : response} */
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "post_game_quiz_responses")
    private Map<Integer, Integer> postGameQuizResponses = new HashMap<>();

    /**
     * Creates an empty research record for a session. Only the association is set; the ID is derived from it
     * when the row is saved, so Spring Data treats the entity as new and inserts it.
     */
    public DataCollection(Session session) {
        this.session = session;
        this.playerUuid = session.getPlayerUuid();
    }
}