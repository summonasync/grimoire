import type {
  MemoryActor,
  MemoryKind,
  MemoryRecord,
  MemoryScope,
  MemoryStatus,
} from "@/shared/api/types";

export type MemoryStatusFilter = MemoryStatus[] | "all";

export interface MemoryListParams {
  query?: string;
  scope?: MemoryScope[];
  kind?: MemoryKind;
  status?: MemoryStatusFilter;
  tag?: string[];
  projectId?: string;
  sessionId?: string;
  tome?: string;
  limit?: number;
}

export interface MemoryListResult {
  tome: string;
  count: number;
  items: MemoryRecord[];
}

export interface CreateMemoryInput {
  content: string;
  scope: MemoryScope;
  kind?: MemoryKind;
  tome?: string;
  projectId?: string;
  sessionId?: string;
  tags?: string[];
  importance?: number;
  sources?: string[];
}

export interface CreateMemoryResult {
  record: MemoryRecord;
  tome: string;
  supersededId: string | null;
}

export interface PatchMemoryInput {
  content?: string;
  tags?: string[];
  importance?: number;
  reason: string;
}

export interface ForgetMemoryInput {
  reason: string;
  tome?: string;
}

export interface SupersederState {
  id: string;
  status: MemoryStatus;
  tombstoneReason: string | null;
}

export interface RestoreMemoryInput {
  reason: string;
  tome?: string;
}

export interface RestoreMemoryResult {
  record: MemoryRecord;
  tome: string;
  restored: boolean;
  superseder: SupersederState | null;
}

export interface HistoryEntry {
  id: string;
  status: MemoryStatus;
  tome: string;
  createdAt: string;
  tombstonedAt: string | null;
  tombstoneReason: string | null;
  actor: MemoryActor;
  bodyPreview: string;
}

export interface HistoryResult {
  tome: string;
  chain: HistoryEntry[];
}
