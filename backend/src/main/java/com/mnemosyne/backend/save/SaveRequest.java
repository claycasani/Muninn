package com.mnemosyne.backend.save;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Pattern;

public record SaveRequest(
        @NotBlank @Pattern(regexp = "link|image") String type,
        String sourceUrl,
        String imageRef
) {}
