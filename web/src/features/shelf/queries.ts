import { fetchTomes } from "./api";

export const shelfKeys = {
  all: ["tomes"] as const,
  census: () => [...shelfKeys.all, "census"] as const,
};

export function tomesQueryOptions() {
  return {
    queryKey: shelfKeys.census(),
    queryFn: fetchTomes,
    staleTime: 0,
  };
}
