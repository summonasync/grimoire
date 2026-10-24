import { apiFetch, jsonRequest } from "@/shared/api/client";
import type {
  ForgetTomeInput,
  MergeTomeInput,
  RenameTomeInput,
  RetomeRecordsInput,
  RetomeRecordsResult,
  TomeForgetResult,
  TomeRetomeResult,
  TomesResult,
} from "./types";

export function fetchTomes(): Promise<TomesResult> {
  return apiFetch<TomesResult>("/api/tomes");
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
