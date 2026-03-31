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
import ltss.service.enums.SurveyType;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "survey_templates",
        indexes = {
                @Index(name = "idx_survey_template_session_definition_id", columnList = "session_definition_id"),
                @Index(name = "idx_survey_template_type", columnList = "type")
        }
)
public class SurveyTemplate extends CreatedUpdatedAtEntity {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY, optional = false)
    @JoinColumn(name = "session_definition_id", nullable = false)
    private ExperimentSessionDefinition sessionDefinition;

    @Column(nullable = false, length = 120)
    private String code;

    @Column(nullable = false, length = 255)
    private String title;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 20)
    private SurveyType type;

    @Column(nullable = false)
    private Integer version;

    @Column(name = "template_json", nullable = false, columnDefinition = "longtext")
    private String templateJson;

    @Column(nullable = false)
    private boolean enabled;
}
