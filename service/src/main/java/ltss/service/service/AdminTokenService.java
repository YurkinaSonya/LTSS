package ltss.service.service;

import java.nio.charset.StandardCharsets;
import java.security.InvalidKeyException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Instant;
import java.util.Base64;
import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import ltss.service.exception.UnauthorizedException;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

@Service
public class AdminTokenService {

    private static final String HMAC_ALGORITHM = "HmacSHA256";

    private final String secret;

    public AdminTokenService(@Value("${app.admin-auth.token-secret:ltss-admin-token-secret}") String secret) {
        this.secret = secret;
    }

    public String createToken(Long adminUserId, String username) {
        String payload = adminUserId + ":" + username + ":" + Instant.now().getEpochSecond();
        String encodedPayload = Base64.getUrlEncoder().withoutPadding()
                .encodeToString(payload.getBytes(StandardCharsets.UTF_8));
        return encodedPayload + "." + sign(encodedPayload);
    }

    public AdminTokenPayload parseToken(String token) {
        if (token == null || token.isBlank()) {
            throw new UnauthorizedException("Missing admin auth token");
        }

        String[] parts = token.split("\\.");
        if (parts.length != 2) {
            throw new UnauthorizedException("Invalid admin auth token");
        }

        String expectedSignature = sign(parts[0]);
        if (!MessageDigest.isEqual(expectedSignature.getBytes(StandardCharsets.UTF_8),
                parts[1].getBytes(StandardCharsets.UTF_8))) {
            throw new UnauthorizedException("Invalid admin auth token");
        }

        String payload = new String(Base64.getUrlDecoder().decode(parts[0]), StandardCharsets.UTF_8);
        String[] values = payload.split(":");
        if (values.length != 3) {
            throw new UnauthorizedException("Invalid admin auth token");
        }

        return new AdminTokenPayload(Long.parseLong(values[0]), values[1], Long.parseLong(values[2]));
    }

    private String sign(String payload) {
        try {
            Mac mac = Mac.getInstance(HMAC_ALGORITHM);
            mac.init(new SecretKeySpec(secret.getBytes(StandardCharsets.UTF_8), HMAC_ALGORITHM));
            byte[] signature = mac.doFinal(payload.getBytes(StandardCharsets.UTF_8));
            return Base64.getUrlEncoder().withoutPadding().encodeToString(signature);
        } catch (NoSuchAlgorithmException | InvalidKeyException ex) {
            throw new IllegalStateException("Unable to sign admin auth token", ex);
        }
    }

    public record AdminTokenPayload(Long adminUserId, String username, Long issuedAtEpochSecond) {
    }
}
