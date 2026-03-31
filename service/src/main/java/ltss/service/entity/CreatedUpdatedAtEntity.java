package ltss.service.entity;

import jakarta.persistence.Column;
import jakarta.persistence.MappedSuperclass;
import jakarta.persistence.PrePersist;
import jakarta.persistence.PreUpdate;
import java.time.LocalDateTime;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
@MappedSuperclass
public abstract class CreatedUpdatedAtEntity extends CreatedAtEntity {

    @Column(name = "updated_at", nullable = false)
    private LocalDateTime updatedAt;

    @Override
    @PrePersist
    protected void onCreate() {
        super.onCreate();
        if (getUpdatedAt() == null) {
            setUpdatedAt(LocalDateTime.now());
        }
    }

    @PreUpdate
    protected void onUpdate() {
        setUpdatedAt(LocalDateTime.now());
    }
}
