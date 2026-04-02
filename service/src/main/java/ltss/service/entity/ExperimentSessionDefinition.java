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
import ltss.service.enums.ExperimentSessionStatus;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "experiment_session_definitions",
        uniqueConstraints = @UniqueConstraint(name = "uk_session_definition_code", columnNames = "code"),
        indexes = {
                @Index(name = "idx_session_definition_code", columnList = "code"),
                @Index(name = "idx_session_definition_status", columnList = "status"),
                @Index(name = "idx_session_definition_statistical_dataset_id", columnList = "statistical_dataset_id")
        }
)
public class ExperimentSessionDefinition extends CreatedUpdatedAtEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, length = 120)
    private String code;

    @Column(nullable = false, length = 255)
    private String title;

    @Column(columnDefinition = "text")
    private String description;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private ExperimentSessionStatus status;

    @Column(name = "config_version", nullable = false)
    private Integer configVersion;

    @Column(name = "participant_count_planned")
    private Integer participantCountPlanned;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "statistical_dataset_id")
    private StatisticalDataset statisticalDataset;

    @Column(name = "session_config_json", nullable = false, columnDefinition = "longtext")
    private String sessionConfigJson;
}