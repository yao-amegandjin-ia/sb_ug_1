package gameapi.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import lombok.AllArgsConstructor;
import lombok.EqualsAndHashCode;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

import java.io.Serializable;

/**
 * Session ID and Researcher ID primary key Tuple.
 * Session ID needs to be a primary key, as Researcher ID is not unique.
 * We still need to distinguish between researchers, so a tuple of both is used as the key
 */
@Embeddable
@Getter @Setter @NoArgsConstructor @AllArgsConstructor @EqualsAndHashCode
public class ResearcherId implements Serializable {

    /** First half of the key; also the foreign key to sessions (mapped by {@code @MapsId} on Researcher). */
    @Column(name = "session_id", length = 36)
    private String sessionId;

    /** Second half of the key: the researcher's number within the session, assigned by the game. */
    @Column(name = "researcher_id")
    private int researcherId;
}