-- --------------------------------------------------------
-- Host:                         127.0.0.1
-- Server version:               11.8.2-MariaDB - mariadb.org binary distribution
-- Server OS:                    Win64
-- HeidiSQL Version:             12.10.0.7000
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


-- Dumping database structure for lc_consumer
CREATE DATABASE IF NOT EXISTS `lc_consumer` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci */;
USE `lc_consumer`;

-- Dumping structure for table lc_consumer.business_action
CREATE TABLE IF NOT EXISTS `business_action` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'Internal surrogate identifier.',
  `entity_id` varchar(40) NOT NULL COMMENT 'External business entity identifier (for example submission or application id).',
  `def_id` bigint(20) NOT NULL COMMENT 'Workflow definition identifier.',
  `action_code` int(11) NOT NULL COMMENT 'Business action code (example: 100=SendLoginCredentials, 200=SendWarningEmail).',
  `status` int(11) NOT NULL COMMENT 'Action execution state: 1=Pending, 2=Running, 3=Completed, 4=Failed.',
  `started_at` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'Execution start timestamp.',
  `completed_at` datetime(6) DEFAULT NULL COMMENT 'Execution completion timestamp.',
  `result_json` longtext DEFAULT NULL COMMENT 'Structured execution result payload (JSON).',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_business_action` (`def_id`,`entity_id`,`action_code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Idempotent record of business side-effects executed by a consumer for a workflow entity.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.entity
CREATE TABLE IF NOT EXISTS `entity` (
  `id` varchar(42) NOT NULL DEFAULT uuid() COMMENT 'Auto generated GUID',
  `created` datetime NOT NULL DEFAULT current_timestamp(),
  UNIQUE KEY `unq_entity` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='one entity can go through different workflow.. so dont restrict it here.. this is just a common id generator table';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox
CREATE TABLE IF NOT EXISTS `inbox` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT 'Internal surrogate identifier.',
  `ack_guid` varchar(64) NOT NULL COMMENT 'External acknowledgement GUID used for idempotency and correlation.',
  `entity_id` varchar(40) NOT NULL COMMENT 'External business entity identifier (for example submission or application id).See if two different consumers are involved, adn one of the consumer doesn'' t even have this entity refercne, it can for the first time, call a relevant DB which it knows for sure contains the referenced entity_id.. then update it in the internal database (for the consumer database)>.. this is more good, because we know that this workflow was not initated here (if the entity id is not present in the entity table here.. )',
  `kind` tinyint(3) unsigned NOT NULL COMMENT 'Workload kind: 1=Transition, 2=Hook.',
  `consumer_id` int(10) unsigned NOT NULL COMMENT 'Registered consumer identifier.',
  `def_id` bigint(20) unsigned NOT NULL COMMENT 'Workflow definition identifier.',
  `def_version_id` bigint(20) unsigned NOT NULL COMMENT 'Definition version identifier captured when workload was created.',
  `handler_version` int(11) DEFAULT NULL COMMENT 'Pinned consumer handler version used for this workload.',
  `instance_guid` varchar(36) DEFAULT NULL COMMENT 'Workflow instance GUID from engine.',
  `on_success` int(11) DEFAULT NULL COMMENT 'Optional consumer action code to execute after successful processing.',
  `on_failure` int(11) DEFAULT NULL COMMENT 'Optional consumer action code to execute after failed processing.',
  `occurred` datetime NOT NULL COMMENT 'Domain occurrence timestamp from source event.',
  `event_code` int(11) DEFAULT NULL COMMENT 'Transition event code; NULL when workload kind is Hook.',
  `route` varchar(200) DEFAULT NULL COMMENT 'Hook route name; NULL when workload kind is Transition.',
  `created` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was created.',
  `handler_upgrade` tinyint(4) NOT NULL DEFAULT 1 COMMENT 'Handler strategy: 1=PinnedVersion, 2=AllowUpgradeToLatest.',
  `run_count` int(11) NOT NULL DEFAULT 1 COMMENT 'Number of times this workload has been executed/retried.',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_workflow_ack_guid` (`ack_guid`),
  KEY `idx_workflow_entity_def` (`entity_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Canonical consumer-side record for one engine-raised transition or hook workload.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox_status
CREATE TABLE IF NOT EXISTS `inbox_status` (
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Workflow record identifier (FK to workflow.id).',
  `params_json` longtext DEFAULT NULL COMMENT 'Incoming parameter payload captured from engine (JSON).',
  `received_at` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the item was received by the consumer.',
  `modified` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was last updated.',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT 'Inbox processing state: 1=Received, 2=Processing, 3=Processed, 4=Failed.',
  `attempt_count` int(10) unsigned NOT NULL DEFAULT 0 COMMENT 'Number of processing attempts performed for this record.',
  `last_error` text DEFAULT NULL COMMENT 'Last captured error message for troubleshooting.',
  PRIMARY KEY (`inbox_id`),
  KEY `idx_inbox_status` (`status`,`received_at`),
  CONSTRAINT `fk_inbox_workflow` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Durable inbound work queue for engine-raised workflow items received by a consumer.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox_step
CREATE TABLE IF NOT EXISTS `inbox_step` (
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Inbox record identifier (FK to inbox.wf_id).',
  `action_code` int(11) NOT NULL DEFAULT 0 COMMENT 'Consumer-defined business action code.',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT 'Step state: 1=Pending, 2=Running, 3=Completed, 4=Failed.',
  `started_at` datetime(6) DEFAULT NULL COMMENT 'Execution start timestamp.',
  `completed_at` datetime(6) DEFAULT NULL COMMENT 'Execution completion timestamp.',
  `result_json` longtext DEFAULT NULL COMMENT 'Structured execution result payload (JSON).',
  `last_error` text DEFAULT NULL COMMENT 'Last captured error message for troubleshooting.',
  PRIMARY KEY (`inbox_id`,`action_code`),
  KEY `idx_inbox_step_status` (`status`),
  CONSTRAINT `fk_inbox_step_inbox` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Step-level execution tracking for inbox items to support deterministic, idempotent processing.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.outbox
CREATE TABLE IF NOT EXISTS `outbox` (
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Inbox record identifier (FK to workflow.id).',
  `current_outcome` tinyint(3) unsigned DEFAULT NULL COMMENT 'Current outcome: 1=Delivered, 2=Processed, 3=Retry, 4=Failed.',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT 'Outbox send state: 1=Pending, 2=Sent, 3=Confirmed, 4=Failed.',
  `next_retry_at` datetime(6) DEFAULT NULL COMMENT 'Scheduled timestamp for next retry attempt.',
  `last_error` text DEFAULT NULL COMMENT 'Last captured error message for troubleshooting.',
  `modified` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was last updated.',
  PRIMARY KEY (`inbox_id`),
  KEY `idx_outbox_send_status` (`status`,`next_retry_at`),
  CONSTRAINT `fk_outbox_workflow` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Outbound acknowledgement queue from consumer to engine with retry scheduling.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.outbox_history
CREATE TABLE IF NOT EXISTS `outbox_history` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT 'Internal surrogate identifier.',
  `outbox_id` bigint(20) unsigned NOT NULL COMMENT 'Outbox record identifier (FK to outbox.wf_id).',
  `outcome` tinyint(3) unsigned NOT NULL COMMENT 'Attempt outcome: 1=Delivered, 2=Processed, 3=Retry, 4=Failed.',
  `status` tinyint(3) unsigned NOT NULL COMMENT 'Attempt send state: 1=Pending, 2=Sent, 3=Confirmed, 4=Failed.',
  `attempt_no` int(10) unsigned NOT NULL COMMENT 'Monotonic attempt sequence number.',
  `modified` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was last updated.',
  `response_payload_json` longtext DEFAULT NULL COMMENT 'Raw response payload captured from engine/API (JSON).',
  `error` text DEFAULT NULL COMMENT 'Error details captured for this send attempt.',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_outbox_attempt` (`outbox_id`,`attempt_no`),
  KEY `idx_outbox_history_outbox_sent` (`outbox_id`,`modified`),
  CONSTRAINT `fk_outbox_history_outbox` FOREIGN KEY (`outbox_id`) REFERENCES `outbox` (`inbox_id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Immutable attempt history for outbox sends, responses, and failures.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.workflow
CREATE TABLE IF NOT EXISTS `workflow` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `entity` varchar(42) NOT NULL COMMENT 'one workflow can have only one entity.. but same entity can be part of multiple workflow.',
  `def_name` varchar(120) NOT NULL,
  `instance_id` varchar(42) NOT NULL,
  `is_triggered` bit(1) NOT NULL DEFAULT b'0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_workflow` (`def_name`,`entity`,`instance_id`),
  KEY `fk_workflow_entity` (`entity`),
  CONSTRAINT `fk_workflow_entity` FOREIGN KEY (`entity`) REFERENCES `entity` (`id`) ON DELETE NO ACTION ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='An entity can be part of different defintion.. for each definitoin it can generate a workflow id.. and we also capture and set the instance id. (coming from the engine)';

-- Data exporting was unselected.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
