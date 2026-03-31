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
        name = "survey_submissions",
        indexes = {
                @Index(name = "idx_survey_submission_run_template_period", columnList = "participant_run_id, survey_template_id, period_number"),
                @Index(name = "idx_survey_submission_survey_template_id", columnList = "survey_template_id"),
                @Index(name = "idx_survey_submission_participant_run_id", columnList = "participant_run_id")
        }
)
public class SurveySubmission extends CreatedAtEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "participant_run_id", nullable = false)
    private ParticipantRun participantRun;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "survey_template_id", nullable = false)
    private SurveyTemplate surveyTemplate;

    @Column(name = "period_number")
    private Integer periodNumber;

    @Column(name = "response_json", nullable = false, columnDefinition = "longtext")
    private String responseJson;

    @Column(name = "submitted_at", nullable = false)
    private LocalDateTime submittedAt;
}
