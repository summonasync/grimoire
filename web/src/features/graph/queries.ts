import { fetchTomeGraph } from "./api";

export const graphKeys = {
  all: ["graph"] as const,
  tome: (tome: string, includeCoMembership: boolean) =>
    [...graphKeys.all, "tome", tome, includeCoMembership] as const,
};

export function tomeGraphQueryOptions(tome: string, includeCoMembership: boolean) {
  return {
    queryKey: graphKeys.tome(tome, includeCoMembership),
    queryFn: () => fetchTomeGraph(tome, includeCoMembership),
  };
}
