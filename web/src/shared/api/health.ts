import { apiFetch } from "./client";

export interface ComponentHealth {
  reachable: boolean;
  error: string | null;
}

export interface HealthResponse {
  status: "healthy" | "degraded";
  store: ComponentHealth;
  embeddings: ComponentHealth;
  checkedAt: string;
}

export const healthKeys = {
  all: ["health"] as const,
};

export function fetchHealth(): Promise<HealthResponse> {
  return apiFetch<HealthResponse>("/health");
}

export function healthQueryOptions() {
  return {
    queryKey: healthKeys.all,
    queryFn: fetchHealth,
  };
}
