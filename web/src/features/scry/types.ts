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
  scope: string;
  kind: string;
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
}
