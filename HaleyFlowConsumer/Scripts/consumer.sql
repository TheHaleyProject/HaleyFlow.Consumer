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

-- Dumping structure for table lc_consumer.inbox
CREATE TABLE IF NOT EXISTS `inbox` (
  `wf_id` bigint(20) unsigned NOT NULL,
  `payload_json` longtext DEFAULT NULL,
  `params_json` longtext DEFAULT NULL,
  `received_at` datetime NOT NULL DEFAULT current_timestamp(),
  `modified` datetime NOT NULL DEFAULT current_timestamp(),
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT '1=Received,2=Processing,3=Processed,4=Failed',
  `attempt_count` int(10) unsigned NOT NULL DEFAULT 0,
  `last_error` text DEFAULT NULL,
  PRIMARY KEY (`wf_id`),
  KEY `idx_inbox_status` (`status`,`received_at`),
  CONSTRAINT `fk_inbox_workflow` FOREIGN KEY (`wf_id`) REFERENCES `workflow` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.inbox_step
CREATE TABLE IF NOT EXISTS `inbox_step` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `inbox_id` bigint(20) unsigned NOT NULL,
  `step_code` int(11) NOT NULL DEFAULT 0 COMMENT 'what is the step that was executed (coming and aware only to the consumer)',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT '1=Pending,2=Running,3=Completed,4=Failed',
  `started_at` datetime(6) DEFAULT NULL,
  `completed_at` datetime(6) DEFAULT NULL,
  `result_json` longtext DEFAULT NULL,
  `last_error` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_inbox_step_code` (`inbox_id`,`step_code`),
  KEY `idx_inbox_step_status` (`status`),
  CONSTRAINT `fk_inbox_step_inbox` FOREIGN KEY (`inbox_id`) REFERENCES `inbox` (`wf_id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.outbox
CREATE TABLE IF NOT EXISTS `outbox` (
  `wf_id` bigint(20) unsigned NOT NULL,
  `current_outcome` tinyint(3) unsigned DEFAULT NULL COMMENT '1=Delivered,2=Processed,3=Retry,4=Failed',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 1 COMMENT '1=Pending,2=Sent,3=Failed',
  `next_retry_at` datetime(6) DEFAULT NULL,
  `last_error` text DEFAULT NULL,
  `modified` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`wf_id`),
  KEY `idx_outbox_send_status` (`status`,`next_retry_at`),
  CONSTRAINT `fk_outbox_workflow` FOREIGN KEY (`wf_id`) REFERENCES `workflow` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.outbox_history
CREATE TABLE IF NOT EXISTS `outbox_history` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `outbox_id` bigint(20) unsigned NOT NULL,
  `outcome` tinyint(3) unsigned NOT NULL COMMENT '1=Delivered,2=Processed,3=Retry,4=Failed',
  `status` tinyint(3) unsigned NOT NULL COMMENT '1=Pending,2=Sent,3=Failed',
  `attempt_no` int(10) unsigned NOT NULL,
  `modified` datetime NOT NULL DEFAULT current_timestamp(),
  `response_payload_json` longtext DEFAULT NULL,
  `error` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_outbox_attempt` (`outbox_id`,`attempt_no`),
  KEY `idx_outbox_history_outbox_sent` (`outbox_id`,`modified`),
  CONSTRAINT `fk_outbox_history_outbox` FOREIGN KEY (`outbox_id`) REFERENCES `outbox` (`wf_id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Data exporting was unselected.

-- Dumping structure for table lc_consumer.workflow
CREATE TABLE IF NOT EXISTS `workflow` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `ack_guid` varchar(64) NOT NULL,
  `entity_id` varchar(160) NOT NULL,
  `kind` tinyint(3) unsigned NOT NULL COMMENT '1=Transition, 2=Hook',
  `consumer_id` int(10) unsigned NOT NULL,
  `def_id` bigint(20) unsigned NOT NULL,
  `def_version_id` bigint(20) unsigned NOT NULL,
  `handler_version` int(11) DEFAULT NULL COMMENT 'pinned on first event.',
  `instance_guid` varchar(36) DEFAULT NULL,
  `on_success` int(11) DEFAULT NULL,
  `on_failure` int(11) DEFAULT NULL,
  `occurred` datetime NOT NULL,
  `event_code` int(11) DEFAULT NULL COMMENT 'Null for hooks.. Which event raised this transition',
  `route` varchar(200) DEFAULT NULL COMMENT 'route that needs to be invoked.. null for transition',
  `created` datetime NOT NULL DEFAULT current_timestamp(),
  `handler_upgrade` tinyint(4) NOT NULL DEFAULT 1 COMMENT '1=Pinned, 2=AllowUpgrade\nIf pinned, the handler has to stick to whatever version is got registered with.\nif allow upgrde, we can allow this to upgrade to latest version presen in the application.. so new steps will be executed.',
  PRIMARY KEY (`id`),
  UNIQUE KEY `unq_workflow_ack_guid` (`consumer_id`,`ack_guid`),
  KEY `idx_workflow_entity_def` (`entity_id`),
  KEY `idx_workflow_consumer_status` (`consumer_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Data exporting was unselected.

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
