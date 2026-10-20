import type { MemoryKind, MemoryScope } from "@/shared/api/types";

export type ScryAttribution = "none" | "light" | "full";

export interface ScryParams {
  query: string;
  tomes?: string[];
  includeJournal?: boolean;
  attribution: ScryAttribution;
}

export interface ScryItem {
  memoryId: string;
  content: string;
  tome: string;
  scope: MemoryScope;
  kind: MemoryKind;
  actor?: string;
  tags: string[];
  projectId: string | null;
  similarity: number;
  score: number;
  createdAt: string;
}

export interface ScryResult {
  tomes: string[];
  attribution: ScryAttribution;
  count: number;
  items: ScryItem[];
  elapsedMs: number;
}
