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
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "event_log_batches",
        indexes = @Index(name = "idx_event_log_batch_participant_run_id", columnList = "participant_run_id")
)
public class EventLogBatch extends CreatedAtEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "participant_run_id", nullable = false)
    private ParticipantRun participantRun;

    @Column(name = "period_number")
    private Integer periodNumber;

    @Column(name = "batch_type", nullable = false, length = 100)
    private String batchType;

    @Column(name = "payload_json", nullable = false, columnDefinition = "longtext")
    private String payloadJson;
}
