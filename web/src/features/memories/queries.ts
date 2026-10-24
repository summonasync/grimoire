import {
  buildMemoryListQuery,
  fetchMemories,
  fetchMemory,
  fetchMemoryHistory,
  fetchRelatedMemories,
} from "./api";
import type { MemoryListParams } from "./types";

export const memoryKeys = {
  all: ["memories"] as const,
  lists: () => [...memoryKeys.all, "list"] as const,
  list: (params: MemoryListParams) => [...memoryKeys.lists(), buildMemoryListQuery(params)] as const,
  detail: (id: string) => [...memoryKeys.all, "detail", id] as const,
  history: (id: string) => [...memoryKeys.detail(id), "history"] as const,
  related: (id: string, includeCoMembership: boolean) =>
    [...memoryKeys.detail(id), "related", includeCoMembership] as const,
};

export function memoriesQueryOptions(params: MemoryListParams = {}) {
  return {
    queryKey: memoryKeys.list(params),
    queryFn: () => fetchMemories(params),
  };
}

export function memoryQueryOptions(id: string) {
  return {
    queryKey: memoryKeys.detail(id),
    queryFn: () => fetchMemory(id),
  };
}

export function memoryHistoryQueryOptions(id: string, tome?: string) {
  return {
    queryKey: memoryKeys.history(id),
    queryFn: () => fetchMemoryHistory(id, tome),
  };
}

export function relatedMemoryQueryOptions(id: string, includeCoMembership = false) {
  return {
    queryKey: memoryKeys.related(id, includeCoMembership),
    queryFn: () => fetchRelatedMemories(id, includeCoMembership),
  };
}
