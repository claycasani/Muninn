package com.mnemosyne.backend.save;

import com.mnemosyne.backend.analysis.Analysis;
import com.mnemosyne.backend.storage.ObjectStorageService;

import java.math.BigDecimal;
import java.time.Duration;
import java.time.LocalDateTime;

public record SaveResponse(
        Long id,
        String type,
        String sourceUrl,
        String imageRef,
        String imageUrl,   // presigned GET URL for image saves; null for link saves
        String status,
        String manualCategory,
        LocalDateTime createdAt,
        LocalDateTime completedAt,
        AnalysisResponse analysis
) {
    static SaveResponse from(Save save, ObjectStorageService storage) {
        Analysis a = save.getAnalysis();
        AnalysisResponse analysisResponse = a == null ? null : new AnalysisResponse(
                a.getArtifactTitle(),
                a.getInferredIntent(),
                a.getCategory(),
                a.getSuggestedAction(),
                a.getExtractedText(),
                a.getConfidence()
        );

        // Generate a short-lived presigned GET URL for image saves so the app
        // can render thumbnails without exposing the R2 bucket publicly.
        String imageUrl = null;
        if ("image".equals(save.getType()) && save.getImageRef() != null) {
            imageUrl = storage.generatePresignedGetUrl(save.getImageRef(), Duration.ofHours(1));
        }

        return new SaveResponse(
                save.getId(),
                save.getType(),
                save.getSourceUrl(),
                save.getImageRef(),
                imageUrl,
                save.getStatus(),
                save.getManualCategory(),
                save.getCreatedAt(),
                save.getCompletedAt(),
                analysisResponse
        );
    }

    public record AnalysisResponse(
            String artifactTitle,
            String inferredIntent,
            String category,
            String suggestedAction,
            String extractedText,
            BigDecimal confidence
    ) {}
}
