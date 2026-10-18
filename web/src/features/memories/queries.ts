import type { MemoryRecord } from "@/shared/api/types";
import { buildMemoryListQuery, fetchMemories, fetchMemory } from "./api";
import type { MemoryListParams } from "./types";

export const memoryKeys = {
  all: ["memories"] as const,
  lists: () => [...memoryKeys.all, "list"] as const,
  list: (params: MemoryListParams) => [...memoryKeys.lists(), buildMemoryListQuery(params)] as const,
  detail: (id: string) => [...memoryKeys.all, "detail", id] as const,
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
