package ltss.service.repository;

import java.util.List;
import java.util.Optional;
import ltss.service.entity.ParticipantAccount;
import org.springframework.data.jpa.repository.JpaRepository;

public interface ParticipantAccountRepository extends JpaRepository<ParticipantAccount, Long> {

    Optional<ParticipantAccount> findByLogin(String login);

    boolean existsByLogin(String login);

    List<ParticipantAccount> findBySessionDefinitionIdOrderByIdAsc(Long sessionDefinitionId);

    long countBySessionDefinitionId(Long sessionDefinitionId);
}
