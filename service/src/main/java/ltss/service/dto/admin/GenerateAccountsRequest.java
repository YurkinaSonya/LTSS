package ltss.service.dto.admin;

import com.fasterxml.jackson.annotation.JsonAlias;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import java.util.List;
import ltss.service.enums.GroupAssignmentStrategy;

public record GenerateAccountsRequest(
        @NotNull(message = "count is required")
        @Min(value = 1, message = "count must be greater than 0")
        Integer count,
        @JsonAlias({"groupAssignmentStrategy", "assignedGroupStrategy"})
        GroupAssignmentStrategy assignedGroupStrategy,
        String prefix,
        List<String> groupCodes,
        String defaultAssignedConfigJson
) {
}
