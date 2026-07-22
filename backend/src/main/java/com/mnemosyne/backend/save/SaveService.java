package com.mnemosyne.backend.save;

import com.mnemosyne.backend.analysis.AnalysisService;
import com.mnemosyne.backend.analysis.AnalysisRepository;
import com.mnemosyne.backend.storage.ObjectStorageService;
import com.mnemosyne.backend.user.User;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.List;

@Service
public class SaveService {

    private final SaveRepository saveRepository;
    private final AnalysisService analysisService;
    private final AnalysisRepository analysisRepository;
    private final ObjectStorageService objectStorageService;

    public SaveService(SaveRepository saveRepository,
                       AnalysisService analysisService,
                       AnalysisRepository analysisRepository,
                       ObjectStorageService objectStorageService) {
        this.saveRepository = saveRepository;
        this.analysisService = analysisService;
        this.analysisRepository = analysisRepository;
        this.objectStorageService = objectStorageService;
    }

    @Transactional
    public Save create(User user, String type, String sourceUrl, String imageRef) {
        Save save = new Save();
        save.setUser(user);
        save.setType(type);
        save.setSourceUrl(sourceUrl);
        save.setImageRef(imageRef);
        Save persisted = saveRepository.save(save);
        // Fire-and-forget — never blocks the HTTP response
        analysisService.analyzeAsync(persisted);
        return persisted;
    }

    public Save findByIdForUser(Long id, String ownerEmail) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return null;
        return save;
    }

    public List<Save> listForUser(User user, String status) {
        if (status != null) {
            return saveRepository.findByUserAndStatusOrderByCreatedAtDesc(user, status);
        }
        return saveRepository.findByUserOrderByCreatedAtDesc(user);
    }

    @Transactional
    public boolean complete(Long id, String ownerEmail) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return false;
        save.setStatus("completed");
        save.setCompletedAt(LocalDateTime.now());
        saveRepository.save(save);
        return true;
    }

    @Transactional
    public boolean archive(Long id, String ownerEmail) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return false;
        save.setStatus("archived");
        saveRepository.save(save);
        return true;
    }

    @Transactional
    public boolean uncomplete(Long id, String ownerEmail) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return false;
        save.setStatus("active");
        save.setCompletedAt(null);
        saveRepository.save(save);
        return true;
    }

    @Transactional
    public boolean unarchive(Long id, String ownerEmail) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return false;
        save.setStatus("active");
        saveRepository.save(save);
        return true;
    }

    @Transactional
    public Save updateCategory(Long id, String ownerEmail, String category) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return null;

        save.setManualCategory(category.trim());
        return saveRepository.save(save);
    }

    @Transactional
    public boolean delete(Long id, String ownerEmail) {
        Save save = saveRepository.findById(id).orElse(null);
        if (save == null || !save.getUser().getEmail().equals(ownerEmail)) return false;

        if (!objectStorageService.deleteObject(save.getImageRef())) {
            throw new IllegalStateException("Couldn't delete the stored image.");
        }

        analysisRepository.deleteBySaveId(id);
        saveRepository.delete(save);
        return true;
    }

    @Transactional
    public int deleteCategory(User user, String category) {
        if (category == null || category.isBlank() || "Uncategorized".equals(category)) return 0;

        String trimmedCategory = category.trim();
        List<Save> saves = saveRepository.findByUserOrderByCreatedAtDesc(user);
        List<Save> matchingSaves = saves.stream()
                .filter(save -> trimmedCategory.equals(effectiveCategory(save)))
                .toList();

        matchingSaves.forEach(save -> save.setManualCategory("Uncategorized"));
        saveRepository.saveAll(matchingSaves);
        return matchingSaves.size();
    }

    /**
     * Rename a category on every save that currently resolves to {@code oldName}.
     * Sets the manual override (which wins over the AI category) so a renamed
     * category moves its saves without touching the AI. No re-analysis.
     */
    @Transactional
    public int relabelCategory(User user, String oldName, String newName) {
        if (oldName == null || newName == null || oldName.equals(newName)) return 0;
        List<Save> matching = saveRepository.findByUserOrderByCreatedAtDesc(user).stream()
                .filter(save -> oldName.equals(effectiveCategory(save)))
                .toList();
        matching.forEach(save -> save.setManualCategory(newName));
        saveRepository.saveAll(matching);
        return matching.size();
    }

    /**
     * Hand every save currently in {@code name} to the AI to re-classify into one
     * of the user's remaining categories. Clears the manual override (so it can no
     * longer point at the deleted category) and fires the async re-categorization
     * per save — never blocks the request.
     */
    @Transactional
    public int recategorizeSavesInCategory(User user, String name, String fallback) {
        if (name == null || name.isBlank()) return 0;
        List<Save> matching = saveRepository.findByUserOrderByCreatedAtDesc(user).stream()
                .filter(save -> name.equals(effectiveCategory(save)))
                .toList();
        for (Save save : matching) {
            // Clear a manual override that pointed at the deleted category.
            if (name.equals(save.getManualCategory())) {
                save.setManualCategory(null);
                saveRepository.save(save);
            }
            String aiCategory = save.getAnalysis() != null ? save.getAnalysis().getCategory() : null;
            boolean stillOrphan = aiCategory == null || aiCategory.isBlank() || name.equals(aiCategory);
            if (!stillOrphan) {
                continue; // the AI category is a still-valid category — nothing to do
            }
            if (save.getAnalysis() != null) {
                analysisService.recategorizeAsync(save); // let the AI pick a remaining one
            } else if (fallback != null && !fallback.isBlank()) {
                save.setManualCategory(fallback); // no analysis text to reason over
                saveRepository.save(save);
            }
        }
        return matching.size();
    }

    private static String effectiveCategory(Save save) {
        if (save.getManualCategory() != null && !save.getManualCategory().isBlank()) {
            return save.getManualCategory();
        }

        if (save.getAnalysis() != null && save.getAnalysis().getCategory() != null && !save.getAnalysis().getCategory().isBlank()) {
            return save.getAnalysis().getCategory();
        }

        return "Uncategorized";
    }
}
