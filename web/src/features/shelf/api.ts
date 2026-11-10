import { apiFetch, jsonRequest } from "@/shared/api/client";
import type {
  CreateTomeManifestInput,
  ForgetTomeInput,
  MergeTomeInput,
  PatchTomeManifestInput,
  RenameTomeInput,
  RetomeRecordsInput,
  RetomeRecordsResult,
  TomeForgetResult,
  TomeManifestResult,
  TomePurgeInput,
  TomePurgeResult,
  TomeRetomeResult,
  TomesResult,
} from "./types";

export function fetchTomes(): Promise<TomesResult> {
  return apiFetch<TomesResult>("/api/tomes");
}

export function createTomeManifest(input: CreateTomeManifestInput): Promise<TomeManifestResult> {
  return apiFetch<TomeManifestResult>(
    "/api/tomes",
    jsonRequest("POST", { key: input.key, description: input.description }),
  );
}

export function patchTomeManifest(input: PatchTomeManifestInput): Promise<TomeManifestResult> {
  return apiFetch<TomeManifestResult>(
    `/api/tomes/${encodeURIComponent(input.tome)}/manifest`,
    jsonRequest("PATCH", { description: input.description }),
  );
}

export function purgeTome(input: TomePurgeInput): Promise<TomePurgeResult> {
  const confirm = new URLSearchParams({ confirm: input.confirm });
  return apiFetch<TomePurgeResult>(
    `/api/tomes/${encodeURIComponent(input.tome)}?${confirm.toString()}`,
    { method: "DELETE" },
  );
}

export function renameTome(input: RenameTomeInput): Promise<TomeRetomeResult> {
  return apiFetch<TomeRetomeResult>(
    `/api/tomes/${encodeURIComponent(input.from)}/rename`,
    jsonRequest("POST", { to: input.to }),
  );
}

export function mergeTome(input: MergeTomeInput): Promise<TomeRetomeResult> {
  return apiFetch<TomeRetomeResult>(
    `/api/tomes/${encodeURIComponent(input.from)}/merge`,
    jsonRequest("POST", { into: input.into }),
  );
}

export function retomeRecords(input: RetomeRecordsInput): Promise<RetomeRecordsResult> {
  return apiFetch<RetomeRecordsResult>(
    "/api/tomes/records/retome",
    jsonRequest("POST", { ids: input.ids, to: input.to }),
  );
}

export function forgetTome(input: ForgetTomeInput): Promise<TomeForgetResult> {
  return apiFetch<TomeForgetResult>(
    `/api/tomes/${encodeURIComponent(input.tome)}/forget`,
    jsonRequest("POST", { reason: input.reason }),
  );
}
