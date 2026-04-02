package ltss.service.entity;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Index;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;
import ltss.service.enums.StatisticalDatasetStatus;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@Entity
@Table(
        name = "statistical_datasets",
        uniqueConstraints = @UniqueConstraint(name = "uk_statistical_dataset_code", columnNames = "code"),
        indexes = {
                @Index(name = "idx_statistical_dataset_code", columnList = "code"),
                @Index(name = "idx_statistical_dataset_status", columnList = "status")
        }
)
public class StatisticalDataset extends CreatedUpdatedAtEntity {

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
    private StatisticalDatasetStatus status;

    @Column(nullable = false)
    private Integer version;

    @Column(name = "dataset_json", nullable = false, columnDefinition = "longtext")
    private String datasetJson;
}