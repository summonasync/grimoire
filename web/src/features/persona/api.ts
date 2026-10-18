import { apiFetch, jsonRequest } from "@/shared/api/client";
import type { PersonaDocument } from "@/shared/api/types";
import type { PersonaWriteResult } from "./types";

export function fetchPersona(): Promise<PersonaDocument> {
  return apiFetch<PersonaDocument>("/api/persona");
}

export function updatePersona(document: PersonaDocument): Promise<PersonaWriteResult> {
  return apiFetch<PersonaWriteResult>("/api/persona", jsonRequest("PUT", document));
}
