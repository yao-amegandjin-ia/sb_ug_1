package gameapi.entity;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.annotations.OnDelete;
import org.hibernate.annotations.OnDeleteAction;
import org.hibernate.type.SqlTypes;

import java.util.HashMap;
import java.util.Map;


/**
 * This provides the Entity Player. This will store the necessary information for player data for the save/load
 * feature of Research Frenzy!
 */
@Entity
@Table(name = "players")
@Getter @Setter @NoArgsConstructor
public class Player {

    @Id
    @Column(name = "player_uuid", length = 36)
    private String playerUuid;

    //May be multiple returning clients per session. We fetch this lazily, as session ID is not needed by game
    @OneToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_id", nullable = false, unique = true)
    @OnDelete(action = OnDeleteAction.CASCADE)
    private Session session;

    @Column(name = "current_money", nullable = false)
    private int currentMoney;

    @Column(name = "total_money_earned", nullable = false)
    private int totalMoneyEarned;

    @Column(name = "current_day", nullable = false)
    private int currentDay = 1;

    //{requestId :methodologyId}
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "study_results")
    private Map<Integer, Integer> studyResults = new HashMap<>();

    //{upgrade :level}
    @JdbcTypeCode(SqlTypes.JSON)
    @Column(name = "owned_upgrades")
    private Map<String, Integer> ownedUpgrades = new HashMap<>();
}