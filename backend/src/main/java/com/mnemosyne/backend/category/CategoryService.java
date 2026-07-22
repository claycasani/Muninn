package com.mnemosyne.backend.category;

import com.mnemosyne.backend.save.SaveService;
import com.mnemosyne.backend.user.User;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

@Service
public class CategoryService {

    private final CategoryRepository categoryRepository;
    private final SaveService saveService;

    public CategoryService(CategoryRepository categoryRepository, SaveService saveService) {
        this.categoryRepository = categoryRepository;
        this.saveService = saveService;
    }

    public List<Category> list(User user) {
        return categoryRepository.findByUserOrderBySortOrderAscNameAsc(user);
    }

    @Transactional
    public Category create(User user, String rawName) {
        String name = normalize(rawName);
        if (categoryRepository.existsByUserAndNameIgnoreCase(user, name)) {
            throw new ResponseStatusException(HttpStatus.CONFLICT, "That category already exists.");
        }
        Category category = new Category();
        category.setUser(user);
        category.setName(name);
        category.setSortOrder(categoryRepository.maxSortOrder(user) + 1);
        return categoryRepository.save(category);
    }

    @Transactional
    public Category rename(User user, Long id, String rawName) {
        Category category = owned(user, id);
        String newName = normalize(rawName);
        String oldName = category.getName();
        if (newName.equalsIgnoreCase(oldName)) {
            category.setName(newName); // allow case-only edits
            return categoryRepository.save(category);
        }
        if (categoryRepository.existsByUserAndNameIgnoreCase(user, newName)) {
            throw new ResponseStatusException(HttpStatus.CONFLICT, "That category already exists.");
        }
        category.setName(newName);
        categoryRepository.save(category);
        // Move every save currently in the old category to the new name (label
        // change only — no re-analysis needed for a rename).
        saveService.relabelCategory(user, oldName, newName);
        return category;
    }

    @Transactional
    public void delete(User user, Long id) {
        Category category = owned(user, id);
        if (categoryRepository.countByUser(user) <= 1) {
            throw new ResponseStatusException(HttpStatus.CONFLICT,
                    "You need at least one category.");
        }
        String name = category.getName();
        categoryRepository.delete(category);
        // Fallback for saves with no analysis text to reason over: prefer "Other",
        // else the first remaining category.
        List<Category> remaining = categoryRepository.findByUserOrderBySortOrderAscNameAsc(user);
        String fallback = remaining.stream().map(Category::getName)
                .filter(n -> n.equalsIgnoreCase("Other")).findFirst()
                .orElseGet(() -> remaining.isEmpty() ? "Uncategorized" : remaining.get(0).getName());
        // Saves in the deleted category are re-classified by the AI into a remaining
        // one (async, per save); the fallback covers analysis-less saves.
        saveService.recategorizeSavesInCategory(user, name, fallback);
    }

    private Category owned(User user, Long id) {
        Category category = categoryRepository.findById(id).orElse(null);
        if (category == null || category.getUser() == null
                || !category.getUser().getId().equals(user.getId())) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Category not found.");
        }
        return category;
    }

    private static String normalize(String raw) {
        String name = raw == null ? "" : raw.trim();
        if (name.isEmpty()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Category name is required.");
        }
        if (name.length() > 80) name = name.substring(0, 80);
        return name;
    }
}
