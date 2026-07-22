package com.mnemosyne.backend.save;

import com.mnemosyne.backend.user.User;
import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface SaveRepository extends JpaRepository<Save, Long> {
    List<Save> findByUserAndStatusOrderByCreatedAtDesc(User user, String status);
    List<Save> findByUserOrderByCreatedAtDesc(User user);
    void deleteByUser(User user);
}
