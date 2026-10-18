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
