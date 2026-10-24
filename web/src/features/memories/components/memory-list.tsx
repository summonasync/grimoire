"use client";

import type { MemoryRecord } from "@/shared/api/types";
import { MemoryListRow } from "./memory-list-row";

export interface MemoryListSelection {
  ids: ReadonlySet<string>;
  onToggle: (id: string) => void;
}

interface MemoryListProps {
  records: MemoryRecord[];
  selectedId: string | null;
  onOpen: (id: string) => void;
  selection?: MemoryListSelection;
}

const columns = ["Content", "Kind", "Scope", "Status", "Actor", "Created", "Imp."] as const;

export function MemoryList({ records, selectedId, onOpen, selection }: MemoryListProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-left text-sm">
        <thead>
          <tr className="border-b border-border-strong/70">
            {selection && <th className="w-8 px-3 py-2" aria-label="Select records" />}
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
              checked={selection?.ids.has(record.id) ?? false}
              onToggle={selection ? selection.onToggle : undefined}
            />
          ))}
        </tbody>
      </table>
    </div>
  );
}
