package com.mnemosyne.backend.analysis;

import com.google.genai.Client;
import com.google.genai.types.Blob;
import com.google.genai.types.Content;
import com.google.genai.types.GenerateContentConfig;
import com.google.genai.types.GenerateContentResponse;
import com.google.genai.types.Part;
import com.mnemosyne.backend.category.CategoryRepository;
import com.mnemosyne.backend.save.Save;
import com.mnemosyne.backend.storage.ObjectStorageService;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Service;

import java.math.BigDecimal;
import java.util.List;

@Service
public class GeminiAnalysisService implements AnalysisService {

    private static final Logger log = LoggerFactory.getLogger(GeminiAnalysisService.class);

    private static final String SYSTEM_PROMPT = """
            You are a save-intent classifier. Given the content of a webpage, return ONLY valid JSON with no preamble, no markdown fences, and no explanation.
            The card title identifies WHAT was saved. It must be a concise artifact label, not a sentence about the user's motivation.
            Never start the card title with phrases like "The user saved", "The user likely", "They probably", or "This page was saved".
            Good card titles: "Job Listing for Product Designer at Linear", "Usage-Based Pricing Guide", "Reservation Page for Kann", "Portable Monitor to Compare".
            The inferred intent explains WHY the user probably saved it. Keep that reasoning out of the card title.
            The JSON must have exactly these five fields:
            {
              "artifactTitle": "<short noun phrase identifying the saved artifact, max 80 chars>",
              "inferredIntent": "<one sentence: why the user probably saved this>",
              "category": "<exactly one of: __CATEGORIES__>",
              "suggestedAction": "<one short imperative sentence: what the user should do with this>",
              "extractedText": "<the main readable text from the page, max 500 chars>"
            }
            """;

    private static final String IMAGE_SYSTEM_PROMPT = """
            You are a save-intent classifier for screenshots. The user has saved a screenshot because it contains something they want to act on.
            Analyze both the visual content and any text visible in the screenshot.
            Return ONLY valid JSON with no preamble, no markdown fences, and no explanation.
            The card title identifies WHAT the user most likely wanted to remember from the screenshot. It must be a concise artifact label, not a sentence about the user's motivation and not merely the container UI.
            For screenshots, do not default to generic container titles like "TikTok comment section", "Instagram post", "Messages conversation", or "Website screenshot" if a specific item/advice/product/place/problem is visible. Title the likely useful artifact instead.
            Examples: "Product for Frizzy Hair", "Solution to Frizzy Hair Problem", "Job Listing for Product Designer at Linear", "Restaurant Recommendation in Portland".
            Never start the card title with phrases like "The user saved", "The user likely", "They probably", or "This screenshot was saved".
            The inferred intent explains WHY the user probably saved it. Keep that reasoning out of the card title.
            The JSON must have exactly these five fields:
            {
              "artifactTitle": "<short noun phrase identifying the saved artifact, max 80 chars>",
              "inferredIntent": "<one sentence: why the user probably saved this screenshot>",
              "category": "<exactly one of: __CATEGORIES__>",
              "suggestedAction": "<one short imperative sentence: what the user should do with this>",
              "extractedText": "<all readable text visible in the screenshot, max 500 chars>"
            }
            """;

    // Fallback vocabulary if a user somehow has no categories (should not happen
    // after the V7 seed) — keeps classification working rather than failing.
    private static final List<String> DEFAULT_CATEGORIES = List.of(
            "To read", "Restaurants to try", "To buy", "To watch",
            "Reference", "Inspiration", "Other");

    private final String model;
    private final String apiKey;
    private final AnalysisRepository analysisRepository;
    private final UrlContentFetcher urlContentFetcher;
    private final ObjectStorageService objectStorageService;
    private final CategoryRepository categoryRepository;
    private final ObjectMapper objectMapper = new ObjectMapper();

    public GeminiAnalysisService(
            @Value("${gemini.model:gemini-2.0-flash}") String model,
            @Value("${gemini.api-key:}") String apiKey,
            AnalysisRepository analysisRepository,
            UrlContentFetcher urlContentFetcher,
            ObjectStorageService objectStorageService,
            CategoryRepository categoryRepository) {
        this.model = model;
        this.apiKey = apiKey;
        this.analysisRepository = analysisRepository;
        this.urlContentFetcher = urlContentFetcher;
        this.objectStorageService = objectStorageService;
        this.categoryRepository = categoryRepository;
    }

    /** The user's category names (ordered) for the AI vocabulary, with a safe fallback. */
    private List<String> categoriesFor(Save save) {
        try {
            Long userId = save.getUser().getId();
            List<String> names = categoryRepository.findNamesByUserId(userId);
            if (!names.isEmpty()) return names;
        } catch (Exception e) {
            log.warn("Could not load categories for save {} — using defaults: {}",
                     save.getId(), e.getMessage());
        }
        return DEFAULT_CATEGORIES;
    }

    private static String withCategories(String promptTemplate, List<String> categories) {
        return promptTemplate.replace("__CATEGORIES__", String.join(", ", categories));
    }

    @Override
    @Async("analysisExecutor")
    public void analyzeAsync(Save save) {
        if (apiKey == null || apiKey.isBlank()) {
            log.warn("GEMINI_API_KEY not set — skipping analysis for save {}", save.getId());
            return;
        }

        try {
            Client client = Client.builder().apiKey(apiKey).build();
            GenerateContentResponse response;

            if ("image".equals(save.getType())) {
                response = analyzeImage(client, save);
            } else {
                response = analyzeLink(client, save);
            }

            if (response == null) return; // already logged inside helpers

            String rawJson = response.text();
            if (rawJson == null || rawJson.isBlank()) {
                log.warn("Empty response from Gemini for save {}", save.getId());
                return;
            }

            // Strip any accidental markdown fences
            rawJson = rawJson.strip();
            if (rawJson.startsWith("```")) {
                rawJson = rawJson.replaceAll("^```[a-z]*\\n?", "").replaceAll("```$", "").strip();
            }

            JsonNode json = objectMapper.readTree(rawJson);

            Analysis analysis = new Analysis();
            analysis.setSave(save);
            analysis.setArtifactTitle(truncate(textOf(json, "artifactTitle"), 80));
            analysis.setInferredIntent(textOf(json, "inferredIntent"));
            analysis.setCategory(textOf(json, "category"));
            analysis.setSuggestedAction(textOf(json, "suggestedAction"));
            analysis.setExtractedText(truncate(textOf(json, "extractedText"), 500));
            analysis.setConfidence(BigDecimal.valueOf(0.9)); // Gemini doesn't return confidence; use placeholder

            analysisRepository.save(analysis);
            log.info("Analysis saved for save {} — category: {}", save.getId(), analysis.getCategory());

        } catch (Exception e) {
            log.error("Analysis failed for save {}: {}", save.getId(), e.getMessage(), e);
            // Never propagate — a failed analysis must not affect anything else
        }
    }

    @Override
    @Async("analysisExecutor")
    public void recategorizeAsync(Save save) {
        if (apiKey == null || apiKey.isBlank()) {
            log.warn("GEMINI_API_KEY not set — skipping recategorize for save {}", save.getId());
            return;
        }
        try {
            Analysis analysis = analysisRepository.findBySaveId(save.getId()).orElse(null);
            if (analysis == null) return; // no text to reason over; caller applies a fallback

            List<String> categories = categoriesFor(save);
            String prompt = """
                    A saved item has this metadata:
                    Title: %s
                    Why it was saved: %s
                    Readable text: %s

                    Choose the single best-fitting category for it from this exact list:
                    %s

                    Return ONLY the exact category name from the list, with no punctuation or explanation.
                    """.formatted(
                        nullSafe(analysis.getArtifactTitle()),
                        nullSafe(analysis.getInferredIntent()),
                        nullSafe(analysis.getExtractedText()),
                        String.join(", ", categories));

            Client client = Client.builder().apiKey(apiKey).build();
            GenerateContentResponse response = client.models.generateContent(
                    model, prompt, GenerateContentConfig.builder().build());

            String chosen = matchCategory(response == null ? null : response.text(), categories);
            if (chosen != null) {
                analysis.setCategory(chosen);
                analysisRepository.save(analysis);
                log.info("Recategorized save {} → {}", save.getId(), chosen);
            }
        } catch (Exception e) {
            log.error("Recategorize failed for save {}: {}", save.getId(), e.getMessage());
        }
    }

    /** Maps a free-text model reply to one of the allowed categories. */
    private static String matchCategory(String raw, List<String> categories) {
        if (categories.isEmpty()) return null;
        String cleaned = raw == null ? "" : raw.strip().replaceAll("[\"'.`]", "").trim();
        for (String c : categories) {
            if (c.equalsIgnoreCase(cleaned)) return c;
        }
        String lower = cleaned.toLowerCase();
        for (String c : categories) {
            if (lower.contains(c.toLowerCase())) return c;
        }
        // Fall back to "Other" if present, else the first category — never leave it stuck.
        return categories.stream().filter(c -> c.equalsIgnoreCase("Other"))
                .findFirst().orElse(categories.get(0));
    }

    private static String nullSafe(String s) {
        return s == null ? "" : s;
    }

    // ── Link branch ──────────────────────────────────────────────────────────

    private GenerateContentResponse analyzeLink(Client client, Save save) throws Exception {
        String url = save.getSourceUrl();
        if (url == null || url.isBlank()) {
            log.info("Save {} has no sourceUrl — skipping analysis", save.getId());
            return null;
        }

        UrlContentFetcher.PageContent content = urlContentFetcher.fetch(url);
        String promptText = content.toPromptText();
        if (promptText.isBlank()) {
            log.warn("No content fetched for save {} url {} — skipping", save.getId(), url);
            return null;
        }

        return client.models.generateContent(
                model,
                promptText,
                GenerateContentConfig.builder()
                        .systemInstruction(Content.fromParts(Part.fromText(
                                withCategories(SYSTEM_PROMPT, categoriesFor(save)))))
                        .build());
    }

    // ── Image branch ─────────────────────────────────────────────────────────

    private GenerateContentResponse analyzeImage(Client client, Save save) throws Exception {
        String imageRef = save.getImageRef();
        if (imageRef == null || imageRef.isBlank()) {
            log.warn("Save {} is type=image but has no imageRef — skipping analysis", save.getId());
            return null;
        }

        byte[] imageBytes = objectStorageService.getObjectBytes(imageRef);
        if (imageBytes == null || imageBytes.length == 0) {
            log.warn("Could not fetch image bytes for save {} key '{}' — skipping analysis",
                     save.getId(), imageRef);
            return null;
        }

        String mimeType = mimeTypeFromKey(imageRef);
        log.debug("Analyzing image save {} — key='{}' mimeType='{}' bytes={}",
                  save.getId(), imageRef, mimeType, imageBytes.length);

        // Build a multimodal Content: text prompt + inline image bytes
        Part imagePart = Part.builder()
                .inlineData(Blob.builder()
                        .mimeType(mimeType)
                        .data(imageBytes)
                        .build())
                .build();

        Content userContent = Content.fromParts(
                Part.fromText("Analyze this screenshot and return JSON as instructed."),
                imagePart);

        return client.models.generateContent(
                model,
                userContent,
                GenerateContentConfig.builder()
                        .systemInstruction(Content.fromParts(Part.fromText(
                                withCategories(IMAGE_SYSTEM_PROMPT, categoriesFor(save)))))
                        .build());
    }

    /**
     * Infers the MIME type from the file extension embedded in the R2 object key.
     * Defaults to image/png since Mac + iOS screenshots are PNG unless the user
     * explicitly uploaded a different format.
     */
    private static String mimeTypeFromKey(String key) {
        if (key == null) return "image/png";
        String lower = key.toLowerCase();
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
        if (lower.endsWith(".heic"))                            return "image/heic";
        if (lower.endsWith(".webp"))                            return "image/webp";
        return "image/png";
    }

    private static String textOf(JsonNode node, String field) {
        JsonNode n = node.get(field);
        return (n != null && !n.isNull()) ? n.asText() : null;
    }

    private static String truncate(String text, int max) {
        if (text == null) return null;
        return text.length() <= max ? text : text.substring(0, max);
    }
}
