import type { PersonaDocument } from "@/shared/api/types";

export type PersonaSectionKey = "identity" | "tone" | "preferences" | "rules";

export interface PersonaSectionSpec {
  key: PersonaSectionKey;
  label: string;
  hint: string;
  placeholder: string;
  rows: number;
}

export interface ApiFailure {
  code: string;
  message: string;
}

export interface PersonaWriteResult {
  tome: string;
  document: PersonaDocument;
}
