import { apiFetch } from "@/shared/api/client";
import type { TomesResult } from "./types";

export function fetchTomes(): Promise<TomesResult> {
  return apiFetch<TomesResult>("/api/tomes");
}
