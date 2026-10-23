import { apiFetch, jsonRequest } from "@/shared/api/client";
import type { PersonaDocument } from "@/shared/api/types";
import type { PersonaWriteResult } from "./types";

function tomeQuery(tome?: string | null): string {
  const search = new URLSearchParams();
  if (tome) {
    search.set("tome", tome);
  }
  const query = search.toString();
  return query ? `?${query}` : "";
}

export function fetchPersona(tome?: string | null): Promise<PersonaDocument> {
  return apiFetch<PersonaDocument>(`/api/persona${tomeQuery(tome)}`);
}

export function updatePersona(
  document: PersonaDocument,
  tome?: string | null,
): Promise<PersonaWriteResult> {
  return apiFetch<PersonaWriteResult>(`/api/persona${tomeQuery(tome)}`, jsonRequest("PUT", document));
}
