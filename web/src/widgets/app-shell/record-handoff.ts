let handedOffRecordId: string | null = null;

export function handOffRecord(id: string): void {
  handedOffRecordId = id;
}

export function readHandedOffRecord(): string | null {
  return handedOffRecordId;
}

export function clearHandedOffRecord(): void {
  handedOffRecordId = null;
}
