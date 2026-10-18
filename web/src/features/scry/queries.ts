import { buildScryQuery, fetchScry } from "./api";
import type { ScryParams } from "./types";

export const scryKeys = {
  all: ["scry"] as const,
  search: (params: ScryParams) => [...scryKeys.all, "search", buildScryQuery(params)] as const,
};

export function scryQueryOptions(params: ScryParams) {
  return {
    queryKey: scryKeys.search(params),
    queryFn: () => fetchScry(params),
  };
}
