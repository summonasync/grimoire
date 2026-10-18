import { ApiError, apiFetch, jsonRequest } from "@/shared/api/client";
import type { SettingsResponse } from "@/shared/api/types";
import type { SettingsUpdateOutcome, UpdateSettingsPayload } from "./types";

export function fetchSettings(): Promise<SettingsResponse> {
  return apiFetch<SettingsResponse>("/api/settings");
}

export async function updateSettings(payload: UpdateSettingsPayload): Promise<SettingsUpdateOutcome> {
  try {
    await apiFetch<unknown>("/api/settings", jsonRequest("PUT", payload));
    return { status: "applied" };
  } catch (cause) {
    if (cause instanceof ApiError && cause.status === 501) {
      return { status: "deferred", code: cause.code, message: cause.message };
    }
    if (cause instanceof ApiError) {
      return { status: "failed", code: cause.code, message: cause.message };
    }
    return {
      status: "failed",
      code: "network_error",
      message: cause instanceof Error ? cause.message : "The settings update could not be sent.",
    };
  }
}
