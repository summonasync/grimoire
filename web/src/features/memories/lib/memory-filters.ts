import type { MemoryKind, MemoryScope, MemoryStatus } from "@/shared/api/types";
import type { MemoryListParams, MemoryStatusFilter } from "../types";

export const memoryScopes: MemoryScope[] = ["session", "project", "global", "personal"];

export const memoryKinds: MemoryKind[] = ["journal", "fact"];

export const memoryStatuses: MemoryStatus[] = ["active", "superseded", "tombstoned"];

export const memoryLimitBounds = { min: 1, max: 500 } as const;

export interface MemoryFilters {
  query: string;
  scopes: MemoryScope[];
  kind: MemoryKind | "";
  status: MemoryStatusFilter;
  tags: string;
  tome: string;
  limit: string;
}

export function defaultMemoryFilters(): MemoryFilters {
  return {
    query: "",
    scopes: [],
    kind: "",
    status: ["active"],
    tags: "",
    tome: "",
    limit: "",
  };
}

export function parseTagList(raw: string): string[] {
  return raw
    .split(",")
    .map((tag) => tag.trim())
    .filter((tag) => tag.length > 0);
}

export function toMemoryListParams(filters: MemoryFilters): MemoryListParams {
  const query = filters.query.trim();
  const tags = parseTagList(filters.tags);
  const tome = filters.tome.trim();
  const limit = Number.parseInt(filters.limit, 10);

  return {
    query: query.length > 0 ? query : undefined,
    scope: filters.scopes.length > 0 ? filters.scopes : undefined,
    kind: filters.kind === "" ? undefined : filters.kind,
    status: filters.status,
    tag: tags.length > 0 ? tags : undefined,
    tome: tome.length > 0 ? tome : undefined,
    limit: Number.isNaN(limit) ? undefined : limit,
  };
}

export function toggleStatusFilter(
  current: MemoryStatusFilter,
  status: MemoryStatus,
): MemoryStatusFilter {
  if (current === "all") {
    return memoryStatuses.filter((entry) => entry !== status);
  }
  const next = current.includes(status)
    ? current.filter((entry) => entry !== status)
    : [...current, status];
  return next.length > 0 ? next : "all";
}
