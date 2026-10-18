import { apiFetch, jsonRequest } from "@/shared/api/client";
import type { PersonaDocument } from "@/shared/api/types";

export function fetchPersona(): Promise<PersonaDocument> {
  return apiFetch<PersonaDocument>("/api/persona");
}

export function updatePersona(document: PersonaDocument): Promise<PersonaDocument> {
  return apiFetch<PersonaDocument>("/api/persona", jsonRequest("PUT", document));
}
