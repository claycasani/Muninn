package com.mnemosyne.backend.analysis;

import com.mnemosyne.backend.save.Save;

/**
 * Abstraction layer for AI-powered save analysis.
 * Swap providers by replacing the implementation — callers never change.
 */
public interface AnalysisService {

    /**
     * Asynchronously analyzes a save and persists the result.
     * Must be called after the Save is committed to the DB.
     * Never throws — errors are logged and swallowed.
     */
    void analyzeAsync(Save save);

    /**
     * Asynchronously re-classifies an already-analyzed save into one of the user's
     * CURRENT categories (used after a category is deleted). Cheap: reuses the
     * existing analysis text instead of re-fetching the page/re-running vision, and
     * updates only the category. Never throws.
     */
    void recategorizeAsync(Save save);
}
