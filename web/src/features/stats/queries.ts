import { fetchStats } from "./api";

export const statsKeys = {
  all: ["stats"] as const,
};

export function statsQueryOptions() {
  return {
    queryKey: statsKeys.all,
    queryFn: fetchStats,
  };
}
