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
