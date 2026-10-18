package gameapi.entity;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.type.SqlTypes;

import java.util.ArrayList;
import java.util.List;

/**
 * Researcher Entity for reserach frenzy.
 * We generate all researchers at the start of the game, so we need to keep track of the data
 * for all researchers
 *
 * We index session ID so we can pull all returning clients this way.
 * Clients will be deleted at the end of a game, or when the session is deleted.
 */
@Entity
@Table(name = "researchers")
@Getter @Setter @NoArgsConstructor
public class Researcher {

    /** Composite primary key (session_id, researcher_id). */
    @EmbeddedId
    private ResearcherId id;

    /**
     * Owning session. {@code @MapsId("sessionId")} makes the {@code session_id} column of the composite key
     * double as this foreign key. Lazy because the game never needs the session when loading researchers.
     */
    @MapsId("sessionId")
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id", nullable = false)
    private Session session;

    @Column(name = "name", length = 100, nullable = false)
    private String name;

    /** Appearance part IDs, read in order ( head, torso, legs). */
    @JdbcTypeCode(SqlTypes.JSON)
    private List<Integer> skin = new ArrayList<>();

    private int experience;

    /** Ordered methodology IDs; the index is the level the methodology unlocks at. */
    @JdbcTypeCode(SqlTypes.JSON)
    private List<Integer> methodologies = new ArrayList<>();

    private int speed;

    private int quality;

    /** Cubicle number. 0 = not purchased. */
    @Column(name = "cubicle_position", nullable = false)
    private int position;
}