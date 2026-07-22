package com.mnemosyne.backend.category;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/** Body for both create and rename — a single category name. */
public record CategoryNameRequest(
        @NotBlank @Size(max = 80) String name
) {}
