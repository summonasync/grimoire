import { apiFetch } from "./client";

export interface AgentActivityItem {
  alias: string;
  vendor: string | null;
  recordCount: number;
  lastActivity: string;
  isHuman: boolean;
}

export interface AgentActivityResponse {
  agents: AgentActivityItem[];
  windowDays: number;
}

export const agentsKeys = {
  all: ["agents"] as const,
  recent: (windowDays: number) => [...agentsKeys.all, "recent", windowDays] as const,
};

export function fetchAgents(windowDays?: number): Promise<AgentActivityResponse> {
  const query = windowDays === undefined ? "" : `?windowDays=${windowDays}`;
  return apiFetch<AgentActivityResponse>(`/api/agents${query}`);
}

export function agentsQueryOptions(windowDays?: number) {
  return {
    queryKey: agentsKeys.recent(windowDays ?? 30),
    queryFn: () => fetchAgents(windowDays),
  };
}
