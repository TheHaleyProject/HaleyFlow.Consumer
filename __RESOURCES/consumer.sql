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
CREATE DATABASE IF NOT EXISTS `lc_consumer`; /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci */;
USE `lc_consumer`;

-- Dumping structure for table lc_consumer.business_action
CREATE TABLE IF NOT EXISTS `business_action` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'Internal surrogate identifier.',
  `instance_id` bigint(20) NOT NULL,
  `action_code` int(11) NOT NULL COMMENT 'Business action code (example: 100=SendLoginCredentials, 200=SendWarningEmail).',
  `status` int(11) NOT NULL COMMENT 'Action execution state: 1=Pending, 2=Running, 3=Completed, 4=Failed.',
  `started_at` datetime NOT NULL COMMENT 'Execution start timestamp.',
  `completed_at` datetime(6) DEFAULT NULL COMMENT 'Execution completion timestamp.',
  `result_json` longtext DEFAULT NULL COMMENT 'Structured execution result payload (JSON).',
  `last_error` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_business_action` (`instance_id`,`action_code`),
  CONSTRAINT `fk_business_action_instance` FOREIGN KEY (`instance_id`) REFERENCES `instance` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Idempotent record of business side-effects executed by a consumer for a workflow instance.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox
CREATE TABLE IF NOT EXISTS `inbox` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT 'Internal surrogate identifier.',
  `ack_guid` varchar(64) NOT NULL COMMENT 'External acknowledgement GUID used for idempotency and correlation.',
  `kind` tinyint(3) unsigned NOT NULL COMMENT 'Workload kind: 1=Transition, 2=Hook.',
  `handler_version` int(11) DEFAULT NULL COMMENT 'Pinned consumer handler version used for this workload.',
  `on_success` int(11) DEFAULT NULL COMMENT 'Optional consumer action code to execute after successful processing.',
  `on_failure` int(11) DEFAULT NULL COMMENT 'Optional consumer action code to execute after failed processing.',
  `occurred` datetime NOT NULL COMMENT 'Domain occurrence timestamp from source event.',
  `event_code` int(11) DEFAULT NULL COMMENT 'Transition event code; NULL when workload kind is Hook.',
  `route` varchar(200) DEFAULT NULL COMMENT 'Hook route name; NULL when workload kind is Transition.',
  `created` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was created.',
  `handler_upgrade` tinyint(4) NOT NULL DEFAULT 1 COMMENT 'Handler strategy: 1=PinnedVersion, 2=AllowUpgradeToLatest.',
  `run_count` int(11) NOT NULL DEFAULT 1 COMMENT 'Number of times this workload has been executed/retried.',
  `dispatch_mode` tinyint unsigned NOT NULL DEFAULT 0 COMMENT '0=NormalRun, 1=ValidationMode, 2=TransitionMode',
  `hook_type` tinyint unsigned DEFAULT NULL COMMENT '1=Gate, 0=Effect. NULL for Transition rows.',
  `instance_id` bigint(20) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_inbox_ack_guid` (`ack_guid`),
  KEY `idx_inbox_instance` (`instance_id`),
  KEY `idx_inbox` (`route`),
  KEY `idx_inbox_0` (`kind`),
  CONSTRAINT `fk_inbox_instance` FOREIGN KEY (`instance_id`) REFERENCES `instance` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Canonical consumer-side record for one engine-raised transition or hook workload.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox_action
CREATE TABLE IF NOT EXISTS `inbox_action` (
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Inbox record identifier (FK to inbox.id).',
  `action_id` bigint(20) NOT NULL DEFAULT 0 COMMENT 'Consumer business action',
  `last_error` text DEFAULT NULL COMMENT 'Last captured error message for troubleshooting.',
  `status` tinyint(4) NOT NULL DEFAULT 1 COMMENT '1 - Attempted, 2 - Completed, 3 - Failed',
  PRIMARY KEY (`inbox_id`,`action_id`),
  KEY `fk_inbox_action_business_action` (`action_id`),
  CONSTRAINT `fk_inbox_action_business_action` FOREIGN KEY (`action_id`) REFERENCES `business_action` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION,
  CONSTRAINT `fk_inbox_step_inbox` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Step-level execution tracking for inbox items to support deterministic, idempotent processing.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox_status
CREATE TABLE IF NOT EXISTS `inbox_status` (
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Inbox record identifier (FK to inbox.id).',
  `params_json` longtext DEFAULT NULL COMMENT 'Incoming parameter payload captured from engine (JSON).',
  `received_at` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the item was received by the consumer.',
  `modified` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was last updated.',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT 'Inbox processing state: 1=Received, 2=Processing, 3=Processed, 4=Failed.',
  `attempt_count` int(10) unsigned NOT NULL DEFAULT 0 COMMENT 'Number of processing attempts performed for this record.',
  `last_error` text DEFAULT NULL COMMENT 'Last captured error message for troubleshooting.',
  PRIMARY KEY (`inbox_id`),
  KEY `idx_inbox_status` (`status`,`received_at`),
  CONSTRAINT `fk_inbox_status_inbox` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Durable processing state for engine-raised inbox deliveries received by a consumer.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.instance
CREATE TABLE IF NOT EXISTS `instance` (
  `guid` varchar(42) NOT NULL COMMENT 'instance guid (not generated here.. coming from the engine).. stored here as a duplicate only for proper sql queries and also for generating reports and easy tracking.',
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `def_name` varchar(120) NOT NULL,
  `def_version_value` int(11) NOT NULL DEFAULT 1 COMMENT 'The actuval version value',
  `entity_guid` varchar(42) NOT NULL,
  `created` datetime NOT NULL COMMENT 'When this instance was created at the engine side, mirrored into the consumer database.',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_instance_guid` (`guid`),
  KEY `idx_instance` (`def_name`,`entity_guid`),
  KEY `idx_instance_0` (`entity_guid`,`def_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Consumer-side mirror of engine workflow instances. One row per engine instance guid.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.outbox
CREATE TABLE IF NOT EXISTS `outbox` (
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Inbox record identifier (FK to inbox.id).',
  `current_outcome` tinyint(3) unsigned DEFAULT NULL COMMENT 'Current outcome: 1=Delivered, 2=Processed, 3=Retry, 4=Failed.',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT 'Outbox send state: 1=Pending, 2=Sent, 3=Confirmed, 4=Failed.',
  `next_retry_at` datetime(6) DEFAULT NULL COMMENT 'Scheduled timestamp for next retry attempt.',
  `last_error` text DEFAULT NULL COMMENT 'Last captured error message for troubleshooting.',
  `modified` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was last updated.',
  `next_event` int(11) DEFAULT NULL,
  PRIMARY KEY (`inbox_id`),
  KEY `idx_outbox_send_status` (`status`,`next_retry_at`),
  CONSTRAINT `fk_outbox_inbox` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Outbound acknowledgement queue from consumer to engine with retry scheduling.';

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.outbox_history
CREATE TABLE IF NOT EXISTS `outbox_history` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT 'Internal surrogate identifier.',
  `inbox_id` bigint(20) unsigned NOT NULL COMMENT 'Inbox record identifier (FK to outbox.inbox_id).',
  `outcome` tinyint(3) unsigned NOT NULL COMMENT 'Attempt outcome: 1=Delivered, 2=Processed, 3=Retry, 4=Failed.',
  `status` tinyint(3) unsigned NOT NULL COMMENT 'Attempt send state: 1=Pending, 2=Sent, 3=Confirmed, 4=Failed.',
  `attempt_no` int(10) unsigned NOT NULL COMMENT 'Monotonic attempt sequence number.',
  `modified` datetime NOT NULL DEFAULT current_timestamp() COMMENT 'UTC timestamp when the row was last updated.',
  `response` longtext DEFAULT NULL COMMENT 'Raw response payload captured from engine/API (JSON). response payload _json',
  `error` text DEFAULT NULL COMMENT 'Error details captured for this send attempt.',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_outbox_attempt` (`inbox_id`,`attempt_no`),
  KEY `idx_outbox_history_outbox_sent` (`inbox_id`,`modified`),
  CONSTRAINT `fk_outbox_history_inbox` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Immutable attempt history for outbox sends, responses, and failures.';

-- Data exporting was unselected.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
