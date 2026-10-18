export type MemoryScope = "session" | "project" | "global" | "personal";

export type MemoryKind = "journal" | "fact";

export type MemoryStatus = "active" | "superseded" | "tombstoned";

export interface MemoryActor {
  alias: string;
  vendor: string | null;
}

export interface MemoryRecord {
  id: string;
  content: string;
  scope: MemoryScope;
  kind: MemoryKind;
  actor: MemoryActor;
  projectId: string | null;
  sessionId: string | null;
  tags: string[];
  importance: number;
  createdAt: string;
  lastAccessedAt: string;
  accessCount: number;
  expiresAt: string | null;
  status: MemoryStatus;
  supersededBy: string | null;
  tombstoneReason: string | null;
  tombstonedAt: string | null;
  sources: string[];
}

export interface PersonaDocument {
  identity: string;
  tone: string;
  preferences: string;
  rules: string;
}

export interface RecencyHalfLives {
  sessionDays: number;
  projectDays: number;
  globalDays: number;
  personalDays: number;
}

export interface CompactionOptions {
  minSimilarity: number;
  stalenessDays: number;
  minClusterSize: number;
}

export interface EngineSettings {
  sessionTtlHours: number;
  dedupSimilarityThreshold: number;
  tombstoneRetentionDays: number;
  recallOversampling: number;
  recencyHalfLives: RecencyHalfLives;
  compaction: CompactionOptions;
}

export interface ServeSettings {
  port: number;
  gcIntervalMinutes: number;
}

export interface SettingsPersistence {
  available: boolean;
  reason: string;
  hint: string;
}

export interface SettingsResponse {
  memory: EngineSettings;
  serve: ServeSettings;
  persistence: SettingsPersistence;
  editable: string[];
}

export interface MemoryStats {
  active: number;
  superseded: number;
  tombstoned: number;
  activeByScope: Record<MemoryScope, number>;
  pendingCompaction: number;
}
