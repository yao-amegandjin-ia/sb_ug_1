package gameapi.repository;

import gameapi.entity.DataCollection;
import org.springframework.data.jpa.repository.JpaRepository;

/**
Data base access for data collection
 */
public interface DataCollectionRepository extends JpaRepository<DataCollection, String> {
}