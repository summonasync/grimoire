import type { MemoryKind, MemoryRecord, MemoryScope, MemoryStatus } from "@/shared/api/types";

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
  reason?: string;
}
