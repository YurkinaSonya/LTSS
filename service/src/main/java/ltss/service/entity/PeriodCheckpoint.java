package ltss.service.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.time.LocalDateTime;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "period_checkpoints",
        indexes = {
                @Index(name = "idx_period_checkpoint_participant_run_id", columnList = "participant_run_id"),
                @Index(name = "idx_period_checkpoint_run_period", columnList = "participant_run_id, period_number")
        }
)
public class PeriodCheckpoint extends CreatedAtEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "participant_run_id", nullable = false)
    private ParticipantRun participantRun;

    @Column(name = "period_number", nullable = false)
    private Integer periodNumber;

    @Column(name = "checkpoint_json", nullable = false, columnDefinition = "longtext")
    private String checkpointJson;

    @Column(name = "summary_json", columnDefinition = "longtext")
    private String summaryJson;

    @Column(name = "submitted_at", nullable = false)
    private LocalDateTime submittedAt;
}
