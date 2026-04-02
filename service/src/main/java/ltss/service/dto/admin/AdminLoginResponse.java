package ltss.service.dto.admin;

import ltss.service.enums.AdminRole;

public record AdminLoginResponse(
        String token,
        AdminUserResponse user
) {

    public record AdminUserResponse(
            Long id,
            String username,
            AdminRole role
    ) {
    }
}
