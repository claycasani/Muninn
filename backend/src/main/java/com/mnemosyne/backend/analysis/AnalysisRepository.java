package com.mnemosyne.backend.analysis;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface AnalysisRepository extends JpaRepository<Analysis, Long> {
    Optional<Analysis> findBySaveId(Long saveId);
    void deleteBySaveId(Long saveId);
}
