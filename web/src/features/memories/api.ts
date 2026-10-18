import { apiFetch, jsonRequest } from "@/shared/api/client";
import type { MemoryRecord } from "@/shared/api/types";
import type {
  CreateMemoryInput,
  CreateMemoryResult,
  MemoryListParams,
  PatchMemoryInput,
} from "./types";

export function buildMemoryListQuery(params: MemoryListParams): string {
  const search = new URLSearchParams();
  const setList = (key: string, values?: string[]) => {
    if (values?.length) {
      search.set(key, values.join(","));
    }
  };

  if (params.query) {
    search.set("query", params.query);
  }
  setList("scope", params.scope);
  if (params.kind) {
    search.set("kind", params.kind);
  }
  if (params.status === "all") {
    search.set("status", "all");
  } else {
    setList("status", params.status);
  }
  setList("tag", params.tag);
  if (params.projectId) {
    search.set("projectId", params.projectId);
  }
  if (params.sessionId) {
    search.set("sessionId", params.sessionId);
  }
  if (params.limit !== undefined) {
    search.set("limit", String(params.limit));
  }

  return search.toString();
}

export async function fetchMemories(params: MemoryListParams): Promise<MemoryRecord[]> {
  const query = buildMemoryListQuery(params);
  return apiFetch<MemoryRecord[]>(query ? `/api/memories?${query}` : "/api/memories");
}

export function fetchMemory(id: string): Promise<MemoryRecord> {
  return apiFetch<MemoryRecord>(`/api/memories/${id}`);
}

export function createMemory(input: CreateMemoryInput): Promise<CreateMemoryResult> {
  return apiFetch<CreateMemoryResult>("/api/memories", jsonRequest("POST", input));
}

export function patchMemory(id: string, input: PatchMemoryInput): Promise<MemoryRecord> {
  return apiFetch<MemoryRecord>(`/api/memories/${id}`, jsonRequest("PATCH", input));
}

export function forgetMemory(id: string, reason: string): Promise<MemoryRecord> {
  return apiFetch<MemoryRecord>(`/api/memories/${id}/forget`, jsonRequest("POST", { reason }));
}
