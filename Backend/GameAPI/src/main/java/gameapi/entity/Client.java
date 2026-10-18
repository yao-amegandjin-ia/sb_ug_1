package gameapi.entity;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.hibernate.type.SqlTypes;

import java.util.ArrayList;
import java.util.List;


/**
 * This provides the Entity Client. This will store the necessary information for a client for the save/load
 * feature of Research Frenzy!
 * These are ONLY returning clients of the game. The game is able to generate the other clients based on the
 * seed and requests completed.
 *
 * We index session ID so we can pull all returning clients this way.
 * Clients will be deleted at the end of a game, or when the session is deleted.
 */

@Entity
@Table(name = "clients")
@Getter @Setter @NoArgsConstructor
public class Client {

    /** Composite primary key (session_id, client_id). */
    @EmbeddedId
    private ClientId id;

    /** Owning session; the foreign key shares the {@code session_id} column of the composite key. */
    @MapsId("sessionId")
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id", nullable = false)
    private Session session;

    @Column(name = "request_id", nullable = false)
    private int requestId;

    /** Appearance part IDs, read in order (for example head, torso, legs). */
    @JdbcTypeCode(SqlTypes.JSON)
    private List<Integer> skin = new ArrayList<>();
}