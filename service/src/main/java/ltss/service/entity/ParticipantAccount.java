package ltss.service.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import ltss.service.enums.ParticipantAccountStatus;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "participant_accounts",
        uniqueConstraints = @UniqueConstraint(name = "uk_participant_account_login", columnNames = "login"),
        indexes = {
                @Index(name = "idx_participant_account_session_definition_id", columnList = "session_definition_id"),
                @Index(name = "idx_participant_account_login", columnList = "login"),
                @Index(name = "idx_participant_account_status", columnList = "status")
        }
)
public class ParticipantAccount extends CreatedUpdatedAtEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_definition_id", nullable = false)
    private ExperimentSessionDefinition sessionDefinition;

    @Column(nullable = false, length = 120)
    private String login;

    @Column(name = "password_hash", nullable = false, length = 255)
    private String passwordHash;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private ParticipantAccountStatus status;

    @Column(name = "assigned_group_code", length = 120)
    private String assignedGroupCode;

    @Column(name = "assigned_config_json", columnDefinition = "longtext")
    private String assignedConfigJson;

    @Column(name = "device_binding_json", columnDefinition = "longtext")
    private String deviceBindingJson;
}
