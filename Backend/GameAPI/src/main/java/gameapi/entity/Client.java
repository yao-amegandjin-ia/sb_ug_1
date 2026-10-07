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
 * We index session ID so we can pull all returning clients this way.
 * Don't need to save ALL clients, only returning ones.
 */

@Entity
@Table(name = "clients", indexes = @Index(name = "idx_clients_session", columnList = "session_id"))
@Getter @Setter @NoArgsConstructor
public class Client {

    @Id
    @Column(name = "client_uuid", length = 36)
    private String clientUuid;

    //May be multiple returning clients per session. We fetch this lazily, as session ID is not needed by game
    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private Session session;

    @Column(name = "request_id", nullable = false)
    private int requestId;

    @JdbcTypeCode(SqlTypes.JSON)
    private List<Integer> skin = new ArrayList<>();

    private String name;
}
