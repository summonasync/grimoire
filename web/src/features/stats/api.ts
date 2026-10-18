import { apiFetch } from "@/shared/api/client";
import type { MemoryStats } from "@/shared/api/types";

export function fetchStats(): Promise<MemoryStats> {
  return apiFetch<MemoryStats>("/api/stats");
}
