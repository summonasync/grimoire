import { apiFetch } from "@/shared/api/client";
import type { RecordEdgeClass, TomeGraph } from "./types";

const classCodes: Record<RecordEdgeClass, string> = {
  supersede: "a",
  link: "b",
  similarity: "c",
  coMembership: "d",
};

export function buildGraphClassesParam(includeCoMembership: boolean): string {
  const classes: RecordEdgeClass[] = ["supersede", "link", "similarity"];
  if (includeCoMembership) {
    classes.push("coMembership");
  }
  return classes.map((entry) => classCodes[entry]).join(",");
}

export function fetchTomeGraph(tome: string, includeCoMembership: boolean): Promise<TomeGraph> {
  const search = new URLSearchParams({ classes: buildGraphClassesParam(includeCoMembership) });
  return apiFetch<TomeGraph>(`/api/tomes/${encodeURIComponent(tome)}/graph?${search.toString()}`);
}
