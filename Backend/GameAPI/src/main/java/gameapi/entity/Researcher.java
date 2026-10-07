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

@Entity
@Table(name = "researchers", indexes = @Index(name = "idx_researchers_session", columnList = "session_id"))
@Getter @Setter @NoArgsConstructor
public class Researcher {

    @Id
    @Column(name = "researcher_uuid", length = 36)
    private String researcherUuid;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id", nullable = false)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private Session session;

    //Skins will have IDs, so we go in the list from top to bottom on skins
    @JdbcTypeCode(SqlTypes.JSON)
    private List<Integer> skin = new ArrayList<>();

    private int experience;

    //Methodologies will also have IDs, so we go in the list from top to bottom
    @JdbcTypeCode(SqlTypes.JSON)
    private List<Integer> methodologies = new ArrayList<>();

    private int speed;

    private int quality;

    private String name;
}