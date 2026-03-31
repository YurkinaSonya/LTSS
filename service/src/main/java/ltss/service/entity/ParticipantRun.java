package ltss.service.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.time.LocalDateTime;
import ltss.service.enums.ParticipantRunStatus;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "participant_runs",
        indexes = {
                @Index(name = "idx_participant_run_participant_account_id", columnList = "participant_account_id"),
                @Index(name = "idx_participant_run_status", columnList = "run_status"),
                @Index(name = "idx_participant_run_last_checkpoint_at", columnList = "last_checkpoint_at")
        }
)
public class ParticipantRun extends CreatedUpdatedAtEntity {

    @Id
    @Column(length = 36, nullable = false, updatable = false)
    private String id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "participant_account_id", nullable = false)
    private ParticipantAccount participantAccount;

    @Enumerated(EnumType.STRING)
    @Column(name = "run_status", nullable = false, length = 20)
    private ParticipantRunStatus runStatus;

    @Column(name = "current_period_number", nullable = false)
    private Integer currentPeriodNumber;

    @Column(name = "bootstrap_version", nullable = false)
    private Integer bootstrapVersion;

    @Column(name = "started_at")
    private LocalDateTime startedAt;

    @Column(name = "finished_at")
    private LocalDateTime finishedAt;

    @Column(name = "last_checkpoint_at")
    private LocalDateTime lastCheckpointAt;
}
