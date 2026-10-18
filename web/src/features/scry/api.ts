import { apiFetch } from "@/shared/api/client";
import type { ScryParams, ScryResult } from "./types";

export function buildScryQuery(params: ScryParams): string {
  const search = new URLSearchParams();
  search.set("q", params.query);

  if (params.tomes?.length) {
    search.set("tomes", params.tomes.join(","));
  }
  if (params.includeJournal) {
    search.set("includeJournal", "true");
  }
  search.set("attribution", params.attribution);

  return search.toString();
}

export function fetchScry(params: ScryParams): Promise<ScryResult> {
  return apiFetch<ScryResult>(`/scry?${buildScryQuery(params)}`);
}
