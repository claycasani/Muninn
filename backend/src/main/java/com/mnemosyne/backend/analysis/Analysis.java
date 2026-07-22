package com.mnemosyne.backend.analysis;

import com.mnemosyne.backend.save.Save;
import jakarta.persistence.*;

import java.math.BigDecimal;
import java.time.LocalDateTime;

@Entity
@Table(name = "analyses")
public class Analysis {

    @Id
    @Column(name = "save_id")
    private Long saveId;

    @OneToOne(fetch = FetchType.LAZY)
    @MapsId
    @JoinColumn(name = "save_id")
    private Save save;

    @Column(name = "inferred_intent")
    private String inferredIntent;

    @Column(name = "artifact_title")
    private String artifactTitle;

    @Column(name = "category")
    private String category;

    @Column(name = "suggested_action")
    private String suggestedAction;

    @Column(name = "extracted_text")
    private String extractedText;

    @Column(name = "confidence")
    private BigDecimal confidence;

    @Column(name = "created_at", nullable = false)
    private LocalDateTime createdAt;

    @PrePersist
    protected void onCreate() {
        this.createdAt = LocalDateTime.now();
    }

    public Long getSaveId() { return saveId; }
    public Save getSave() { return save; }
    public void setSave(Save save) { this.save = save; }
    public String getArtifactTitle() { return artifactTitle; }
    public void setArtifactTitle(String artifactTitle) { this.artifactTitle = artifactTitle; }
    public String getInferredIntent() { return inferredIntent; }
    public void setInferredIntent(String inferredIntent) { this.inferredIntent = inferredIntent; }
    public String getCategory() { return category; }
    public void setCategory(String category) { this.category = category; }
    public String getSuggestedAction() { return suggestedAction; }
    public void setSuggestedAction(String suggestedAction) { this.suggestedAction = suggestedAction; }
    public String getExtractedText() { return extractedText; }
    public void setExtractedText(String extractedText) { this.extractedText = extractedText; }
    public BigDecimal getConfidence() { return confidence; }
    public void setConfidence(BigDecimal confidence) { this.confidence = confidence; }
    public LocalDateTime getCreatedAt() { return createdAt; }
}
