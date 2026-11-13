import { apiFetch } from "./client";
import type { SessionsResponse } from "./types";

export const sessionsKeys = {
  all: ["sessions"] as const,
  recent: (windowHours: number | "default") => [...sessionsKeys.all, "recent", windowHours] as const,
};

export interface SessionsQuery {
  limit?: number;
  windowHours?: number;
}

export function fetchSessions(query: SessionsQuery = {}): Promise<SessionsResponse> {
  const search = new URLSearchParams();
  if (query.limit !== undefined) {
    search.set("limit", String(query.limit));
  }
  if (query.windowHours !== undefined) {
    search.set("windowHours", String(query.windowHours));
  }
  const suffix = search.size > 0 ? `?${search.toString()}` : "";
  return apiFetch<SessionsResponse>(`/api/sessions${suffix}`);
}

export function sessionsQueryOptions(windowHours?: number) {
  return {
    queryKey: sessionsKeys.recent(windowHours ?? "default"),
    queryFn: () => fetchSessions({ windowHours }),
  };
}
