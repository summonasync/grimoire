import type { ScryAttribution, ScryParams } from "../types";

export const scryAttributions: ScryAttribution[] = ["none", "light", "full"];

export interface ScryFilters {
  query: string;
  tomes: string;
  attribution: ScryAttribution;
  includeJournal: boolean;
}

export function defaultScryFilters(): ScryFilters {
  return {
    query: "",
    tomes: "",
    attribution: "none",
    includeJournal: false,
  };
}

export function toScryParams(filters: ScryFilters): ScryParams {
  const query = filters.query.trim();
  const tomes = filters.tomes
    .split(",")
    .map((tome) => tome.trim())
    .filter((tome) => tome.length > 0);

  return {
    query,
    tomes: tomes.length > 0 ? tomes : undefined,
    includeJournal: filters.includeJournal ? true : undefined,
    attribution: filters.attribution,
  };
}
