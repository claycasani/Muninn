package com.mnemosyne.backend.save;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.mnemosyne.backend.analysis.Analysis;
import com.mnemosyne.backend.analysis.AnalysisRepository;
import com.mnemosyne.backend.analysis.AnalysisService;
import com.mnemosyne.backend.user.User;
import com.mnemosyne.backend.user.UserRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.boot.test.web.server.LocalServerPort;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;
import org.springframework.http.MediaType;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.web.client.RestClient;

import java.math.BigDecimal;
import java.util.Map;
import java.util.concurrent.TimeUnit;

import static org.assertj.core.api.Assertions.assertThat;
import static org.awaitility.Awaitility.await;

@SpringBootTest(webEnvironment = SpringBootTest.WebEnvironment.RANDOM_PORT)
class AnalysisPipelineIntegrationTest {

    /** Stub that writes an Analysis row after a short delay — no real Gemini call. */
    @TestConfiguration
    static class StubAnalysisConfig {
        @Bean
        @Primary
        AnalysisService stubAnalysisService(AnalysisRepository analysisRepository) {
            // Anonymous class (not a lambda): AnalysisService now has two methods.
            return new AnalysisService() {
                @Override
                public void analyzeAsync(Save save) {
                    new Thread(() -> {
                        try { Thread.sleep(200); } catch (InterruptedException ignored) {}
                        Analysis analysis = new Analysis();
                        analysis.setSave(save);
                        analysis.setArtifactTitle("Example Domain");
                        analysis.setInferredIntent("Test intent");
                        analysis.setCategory("To read");
                        analysis.setSuggestedAction("Read it");
                        analysis.setExtractedText("Stub content");
                        analysis.setConfidence(BigDecimal.valueOf(0.95));
                        analysisRepository.save(analysis);
                    }).start();
                }

                @Override
                public void recategorizeAsync(Save save) {
                    // No-op for the pipeline test.
                }
            };
        }
    }

    @LocalServerPort int port;
    @Autowired JdbcTemplate jdbcTemplate;
    @Autowired AnalysisRepository analysisRepository;

    private RestClient http;
    private String jwt;
    private static final String TEST_EMAIL = "pipeline-test@example.com";
    private static final String TEST_PASSWORD = "password123";

    @BeforeEach
    void setUp() {
        http = RestClient.builder()
                .baseUrl("http://localhost:" + port)
                .build();

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

        Map<?, ?> body = http.post().uri("/auth/register")
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("email", TEST_EMAIL, "password", TEST_PASSWORD))
                .retrieve()
                .body(Map.class);

        jwt = (String) body.get("token");
    }

    @Test
    void postSave_triggersAsyncAnalysis_andAnalysisRowAppearsInDb() {
        Map<?, ?> saveResp = http.post().uri("/saves")
                .header("Authorization", "Bearer " + jwt)
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("type", "link", "sourceUrl", "https://example.com"))
                .retrieve()
                .body(Map.class);

        long saveId = ((Number) saveResp.get("id")).longValue();

        await().atMost(5, TimeUnit.SECONDS)
                .pollInterval(200, TimeUnit.MILLISECONDS)
                .until(() -> analysisRepository.findBySaveId(saveId).isPresent());

        Analysis analysis = analysisRepository.findBySaveId(saveId).orElseThrow();
        assertThat(analysis.getArtifactTitle()).isEqualTo("Example Domain");
        assertThat(analysis.getCategory()).isEqualTo("To read");
        assertThat(analysis.getInferredIntent()).isNotBlank();

        Map<?, ?> categoryResp = http.put().uri("/saves/{id}/category", saveId)
                .header("Authorization", "Bearer " + jwt)
                .contentType(MediaType.APPLICATION_JSON)
                .body(Map.of("category", "Research"))
                .retrieve()
                .body(Map.class);
        assertThat(categoryResp.get("manualCategory")).isEqualTo("Research");

        http.delete().uri("/saves/categories?category={category}", "Research")
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .toBodilessEntity();

        Map<?, ?> uncategorizedResp = http.get().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .body(Map.class);
        assertThat(uncategorizedResp.get("manualCategory")).isEqualTo("Uncategorized");

        http.post().uri("/saves/{id}/complete", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .toBodilessEntity();

        Map<?, ?> completedResp = http.get().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .body(Map.class);
        assertThat(completedResp.get("status")).isEqualTo("completed");
        assertThat(completedResp.get("completedAt")).isNotNull();

        http.post().uri("/saves/{id}/uncomplete", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .toBodilessEntity();

        Map<?, ?> activeResp = http.get().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .body(Map.class);
        assertThat(activeResp.get("status")).isEqualTo("active");
        assertThat(activeResp.get("completedAt")).isNull();

        http.post().uri("/saves/{id}/archive", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .toBodilessEntity();

        Map<?, ?> archivedResp = http.get().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .body(Map.class);
        assertThat(archivedResp.get("status")).isEqualTo("archived");

        http.post().uri("/saves/{id}/unarchive", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .toBodilessEntity();

        Map<?, ?> unarchivedResp = http.get().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .body(Map.class);
        assertThat(unarchivedResp.get("status")).isEqualTo("active");

        http.delete().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .retrieve()
                .toBodilessEntity();

        var deletedStatus = http.get().uri("/saves/{id}", saveId)
                .header("Authorization", "Bearer " + jwt)
                .exchange((request, response) -> response.getStatusCode());
        assertThat(deletedStatus.value()).isEqualTo(404);
        assertThat(analysisRepository.findBySaveId(saveId)).isEmpty();
    }
}
