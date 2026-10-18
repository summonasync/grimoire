import type { CompactionOptions, RecencyHalfLives } from "@/shared/api/types";

export type SettingsFieldKey =
  | "sessionTtlHours"
  | "dedupSimilarityThreshold"
  | "tombstoneRetentionDays"
  | "recallOversampling"
  | "halfLives.sessionDays"
  | "halfLives.projectDays"
  | "halfLives.globalDays"
  | "halfLives.personalDays"
  | "compaction.minSimilarity"
  | "compaction.stalenessDays"
  | "compaction.minClusterSize";

export type SettingsDraft = Record<SettingsFieldKey, string>;

export interface UpdateSettingsPayload {
  sessionTtlHours: number;
  dedupSimilarityThreshold: number;
  tombstoneRetentionDays: number;
  recallOversampling: number;
  recencyHalfLives: RecencyHalfLives;
  compaction: CompactionOptions;
}

export type SettingsUpdateOutcome =
  | { status: "applied" }
  | { status: "deferred"; code: string; message: string }
  | { status: "failed"; code: string; message: string };

export interface ApiFailure {
  code: string;
  message: string;
}
