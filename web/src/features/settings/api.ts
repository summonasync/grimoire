import { apiFetch } from "@/shared/api/client";
import type { SettingsResponse } from "@/shared/api/types";

export function fetchSettings(): Promise<SettingsResponse> {
  return apiFetch<SettingsResponse>("/api/settings");
}
