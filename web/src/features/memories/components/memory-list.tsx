"use client";

import type { MemoryRecord } from "@/shared/api/types";
import { MemoryListRow } from "./memory-list-row";

interface MemoryListProps {
  records: MemoryRecord[];
  selectedId: string | null;
  onOpen: (id: string) => void;
}

export function MemoryList({ records, selectedId, onOpen }: MemoryListProps) {
  return (
    <ul className="space-y-2">
      {records.map((record) => (
        <MemoryListRow
          key={record.id}
          record={record}
          selected={record.id === selectedId}
          onOpen={onOpen}
        />
      ))}
    </ul>
  );
}
