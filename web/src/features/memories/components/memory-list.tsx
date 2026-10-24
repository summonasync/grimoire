"use client";

import type { MemoryRecord } from "@/shared/api/types";
import { MemoryListRow } from "./memory-list-row";

interface MemoryListProps {
  records: MemoryRecord[];
  selectedId: string | null;
  onOpen: (id: string) => void;
}

const columns = ["Content", "Kind", "Scope", "Status", "Project", "Actor", "Created", "Imp."] as const;

export function MemoryList({ records, selectedId, onOpen }: MemoryListProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-left text-sm">
        <thead>
          <tr className="border-b border-border-strong/70">
            {columns.map((column) => (
              <th key={column} className="label px-3 py-2 font-medium whitespace-nowrap">
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {records.map((record) => (
            <MemoryListRow
              key={record.id}
              record={record}
              selected={record.id === selectedId}
              onOpen={onOpen}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
