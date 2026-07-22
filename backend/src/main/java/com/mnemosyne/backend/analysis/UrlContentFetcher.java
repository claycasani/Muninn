package com.mnemosyne.backend.analysis;

import org.jsoup.Jsoup;
import org.jsoup.nodes.Document;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

/**
 * Fetches readable content from a URL using Jsoup.
 * Extracts title, meta description, and visible body text (max 2000 chars).
 */
@Component
public class UrlContentFetcher {

    private static final Logger log = LoggerFactory.getLogger(UrlContentFetcher.class);
    private static final int TIMEOUT_MS = 8_000;
    private static final int MAX_BODY_CHARS = 2_000;

    public record PageContent(String title, String description, String bodyText) {
        /** Compact representation sent to the LLM. */
        public String toPromptText() {
            StringBuilder sb = new StringBuilder();
            if (title != null && !title.isBlank()) sb.append("Title: ").append(title).append("\n");
            if (description != null && !description.isBlank()) sb.append("Description: ").append(description).append("\n");
            if (bodyText != null && !bodyText.isBlank()) sb.append("Content:\n").append(bodyText);
            return sb.toString().strip();
        }
    }

    public PageContent fetch(String url) {
        try {
            Document doc = Jsoup.connect(url)
                    .userAgent("Mozilla/5.0 (compatible; Muninn-bot/1.0)")
                    .timeout(TIMEOUT_MS)
                    .get();

            String title = doc.title();
            String description = doc.select("meta[name=description]").attr("content");
            if (description.isBlank()) {
                description = doc.select("meta[property=og:description]").attr("content");
            }

            // Prefer article body, fall back to all visible text
            String bodyText = doc.select("article, main, [role=main]").text();
            if (bodyText.isBlank()) {
                bodyText = doc.body().text();
            }
            if (bodyText.length() > MAX_BODY_CHARS) {
                bodyText = bodyText.substring(0, MAX_BODY_CHARS);
            }

            return new PageContent(title, description, bodyText);
        } catch (Exception e) {
            log.warn("Failed to fetch URL content for {}: {}", url, e.getMessage());
            return new PageContent(url, null, null);
        }
    }
}
