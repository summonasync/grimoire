export interface TomeSummary {
  key: string;
  active: number;
  tombstoned: number;
  lastWriteAt: string;
  own: boolean;
}

export interface TomesResult {
  tome: string;
  tomes: TomeSummary[];
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
