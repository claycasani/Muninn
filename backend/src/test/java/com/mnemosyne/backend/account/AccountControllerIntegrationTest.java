package com.mnemosyne.backend.account;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.web.client.RestClient;

import java.util.Map;

import static org.assertj.core.api.Assertions.assertThat;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
class AccountControllerIntegrationTest {

    private static final String TEST_EMAIL = "account-test@example.com";
    private static final String OLD_PASSWORD = "old-password-1!";
    private static final String NEW_PASSWORD = "new-password-2!";
    private static final String TEST_URL = "https://example.com/account-delete-test";

    @LocalServerPort int port;
    @Autowired JdbcTemplate jdbcTemplate;

    private RestClient http;

    @BeforeEach
    void setUp() {
        http = RestClient.builder()
                .baseUrl("http://localhost:" + port)
                .build();
        cleanup();
    }

    @AfterEach
    void tearDown() {
        cleanup();
    }

    @Test
    void accountEndpoints_changePasswordAndDeleteAccount() {
        String jwt = register(TEST_EMAIL, OLD_PASSWORD);

        Map<?, ?> account = http.get().uri("/account")
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .body(Map.class);

        assertThat(account.get("email")).isEqualTo(TEST_EMAIL);
        assertThat(account.get("displayName")).isEqualTo("Account Test");
        assertThat(account.get("dailyDigestEnabled")).isEqualTo(true);
        assertThat(account.get("digestTime")).isEqualTo("07:00");
        assertThat(account.get("notificationsEnabled")).isEqualTo(true);
        assertThat(account.get("autoArchiveDays")).isEqualTo(30);

        Map<?, ?> updated = http.put().uri("/account")
                .header("Authorization", "Bearer " + jwt)
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of(
                        "displayName", "Clay Test",
                        "dailyDigestEnabled", false,
                        "digestTime", "18:00",
                        "notificationsEnabled", false,
                        "autoArchiveDays", 60
                ))
                .retrieve()
                .body(Map.class);

        assertThat(updated.get("displayName")).isEqualTo("Clay Test");
        assertThat(updated.get("dailyDigestEnabled")).isEqualTo(false);
        assertThat(updated.get("digestTime")).isEqualTo("18:00");
        assertThat(updated.get("notificationsEnabled")).isEqualTo(false);
        assertThat(updated.get("autoArchiveDays")).isEqualTo(60);

        int wrongPasswordStatus = http.post().uri("/account/password")
                .header("Authorization", "Bearer " + jwt)
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("currentPassword", "wrong-password", "newPassword", NEW_PASSWORD))
                .exchange((request, response) -> response.getStatusCode().value());

        assertThat(wrongPasswordStatus).isEqualTo(403);

        int changeStatus = http.post().uri("/account/password")
                .header("Authorization", "Bearer " + jwt)
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("currentPassword", OLD_PASSWORD, "newPassword", NEW_PASSWORD))
                .exchange((request, response) -> response.getStatusCode().value());

        assertThat(changeStatus).isEqualTo(204);
        assertThat(loginStatus(TEST_EMAIL, OLD_PASSWORD)).isEqualTo(401);

        String newJwt = registerLogin(TEST_EMAIL, NEW_PASSWORD);
        jdbcTemplate.update("""
                INSERT INTO saves (user_id, type, source_url, status)
                SELECT id, 'link', ?, 'active'
                FROM users WHERE email = ?
                """, TEST_URL, TEST_EMAIL);

        int deleteStatus = http.delete().uri("/account")
                .header("Authorization", "Bearer " + newJwt)
                .exchange((request, response) -> response.getStatusCode().value());

        assertThat(deleteStatus).isEqualTo(204);
        assertThat(jdbcTemplate.queryForObject(
                "SELECT COUNT(*) FROM users WHERE email = ?", Integer.class, TEST_EMAIL)).isZero();
        assertThat(jdbcTemplate.queryForObject("""
                SELECT COUNT(*) FROM saves
                WHERE source_url = ?
                """, Integer.class, TEST_URL)).isZero();
    }

    private String register(String email, String password) {
        Map<?, ?> body = http.post().uri("/auth/register")
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("email", email, "password", password))
                .retrieve()
                .body(Map.class);
        return (String) body.get("token");
    }

    private String registerLogin(String email, String password) {
        Map<?, ?> body = http.post().uri("/auth/login")
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("email", email, "password", password))
                .retrieve()
                .body(Map.class);
        return (String) body.get("token");
    }

    private int loginStatus(String email, String password) {
        return http.post().uri("/auth/login")
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("email", email, "password", password))
                .exchange((request, response) -> response.getStatusCode().value());
    }

    private void cleanup() {
        jdbcTemplate.update("""
                DELETE FROM analyses
                WHERE save_id IN (
                    SELECT s.id FROM saves s
                    JOIN users u ON u.id = s.user_id
                    WHERE u.email = ?
                )
                """, TEST_EMAIL);
        jdbcTemplate.update("""
                DELETE FROM saves
                WHERE user_id IN (
                    SELECT id FROM users WHERE email = ?
                )
                """, TEST_EMAIL);
        jdbcTemplate.update("DELETE FROM users WHERE email = ?", TEST_EMAIL);
        jdbcTemplate.update("DELETE FROM saves WHERE source_url = ?", TEST_URL);
    }
}
