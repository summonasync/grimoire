import { apiFetch } from "./client";
import type { VersionResponse } from "./types";

export const versionKeys = {
  all: ["version"] as const,
};

export function fetchVersion(): Promise<VersionResponse> {
  return apiFetch<VersionResponse>("/api/version");
}

export function versionQueryOptions() {
  return {
    queryKey: versionKeys.all,
    queryFn: fetchVersion,
  };
}
