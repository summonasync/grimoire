"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { MemoryRecord } from "@/shared/api/types";
import { memoryHistoryQueryOptions } from "../queries";
import { MemoryStatusBadge } from "./memory-status-badge";

interface MemoryHistorySectionProps {
  record: MemoryRecord;
}

export function MemoryHistorySection({ record }: MemoryHistorySectionProps) {
  const [open, setOpen] = useState(false);
  const history = useQuery({
    ...memoryHistoryQueryOptions(record.id, record.tome),
    enabled: open,
  });

  return (
    <div className="mt-4 border-t border-border pt-4">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        aria-expanded={open}
        className="text-xs font-medium text-muted transition-colors hover:text-foreground"
      >
        {open ? "Hide history" : "History…"}
      </button>

      {open && (
        <div className="mt-3">
          {history.isPending ? (
            <p className="text-xs text-muted">Loading history…</p>
          ) : history.error ? (
            <p className="text-xs text-red-400">{history.error.message}</p>
          ) : history.data.chain.length === 0 ? (
            <p className="text-xs text-muted">No history entries.</p>
          ) : (
            <ol className="space-y-3">
              {history.data.chain.map((entry) => (
                <li
                  key={entry.id}
                  className={
                    entry.id === record.id
                      ? "rounded-lg border border-accent/40 bg-accent/5 p-3"
                      : "rounded-lg border border-border bg-background p-3"
                  }
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <MemoryStatusBadge status={entry.status} />
                    {entry.id === record.id && (
                      <span className="text-xs font-medium text-accent">current</span>
                    )}
                    <span className="ml-auto text-xs text-muted">
                      {new Date(entry.createdAt).toLocaleString()}
                    </span>
                  </div>
                  <p className="mt-2 line-clamp-2 font-mono text-xs break-words">
                    {entry.bodyPreview}
                  </p>
                  <p className="mt-1 text-xs text-muted">
                    {entry.actor.alias}
                    {entry.actor.vendor ? ` · ${entry.actor.vendor}` : ""} · tome {entry.tome}
                  </p>
                  {entry.tombstoneReason && (
                    <p className="mt-2 text-xs text-muted">
                      Tombstone reason: {entry.tombstoneReason}
                      {entry.tombstonedAt
                        ? ` (${new Date(entry.tombstonedAt).toLocaleString()})`
                        : ""}
                    </p>
                  )}
                </li>
              ))}
            </ol>
          )}
        </div>
      )}
    </div>
  );
}
