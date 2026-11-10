export interface TomeSummary {
  key: string;
  active: number;
  tombstoned: number;
  lastWriteAt: string | null;
  description: string | null;
  createdBy: string | null;
  manifest: boolean;
  own: boolean;
}

export interface TomesResult {
  tome: string;
  tomes: TomeSummary[];
}

export interface CreateTomeManifestInput {
  key: string;
  description: string;
}

export interface PatchTomeManifestInput {
  tome: string;
  description: string;
}

export interface TomeManifestResult {
  key: string;
  description: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

export interface TomePurgeInput {
  tome: string;
  confirm: string;
}

export interface TomePurgeCounts {
  active: number;
  tombstoned: number;
  superseded: number;
}

export interface TomePurgeResult {
  key: string;
  purged: TomePurgeCounts;
  manifestPurged: boolean;
}

export interface RenameTomeInput {
  from: string;
  to: string;
}

export interface MergeTomeInput {
  from: string;
  into: string;
}

export interface RetomeRecordsInput {
  ids: string[];
  to: string;
}

export interface ForgetTomeInput {
  tome: string;
  reason: string;
}

export interface TomeRetomeResult {
  from: string;
  to: string;
  retomed: number;
  skipped: number;
}

export interface RetomeRecordsResult {
  retomed: number;
  to: string;
  skipped: number;
}

export interface TomeForgetResult {
  tome: string;
  forgotten: number;
  skipped: number;
}
