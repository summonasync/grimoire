"use client";

import type { MemoryRecord } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";
import { MemoryStatusBadge } from "./memory-status-badge";

interface MemoryListRowProps {
  record: MemoryRecord;
  selected: boolean;
  onOpen: (id: string) => void;
}

export function MemoryListRow({ record, selected, onOpen }: MemoryListRowProps) {
  return (
    <li>
      <button
        type="button"
        onClick={() => onOpen(record.id)}
        aria-expanded={selected}
        className={cn(
          "w-full rounded-lg border px-4 py-3 text-left transition-colors",
          selected
            ? "border-accent bg-accent/5"
            : "border-border bg-surface hover:border-accent/40",
        )}
      >
        <div className="flex items-start justify-between gap-3">
          <p className="line-clamp-2 text-sm whitespace-pre-wrap">{record.content}</p>
          <MemoryStatusBadge status={record.status} />
        </div>
        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
          <span className="rounded bg-background px-1.5 py-0.5 font-medium text-foreground">
            {record.scope}
          </span>
          <span>tome {record.tome}</span>
          <span>{record.kind}</span>
          <span>importance {record.importance.toFixed(2)}</span>
          <span>{record.accessCount} accesses</span>
          <span>created {new Date(record.createdAt).toLocaleString()}</span>
          {record.tags.length > 0 && (
            <span className="flex flex-wrap gap-1">
              {record.tags.map((tag) => (
                <span key={tag} className="rounded-full border border-border px-1.5 py-0.5">
                  #{tag}
                </span>
              ))}
            </span>
          )}
        </div>
      </button>
    </li>
  );
}
