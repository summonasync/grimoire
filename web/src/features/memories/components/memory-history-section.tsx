"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { MemoryRecord } from "@/shared/api/types";
import { StatusBadge } from "@/shared/ui/badge";
import { Icon } from "@/shared/ui/icons";
import { absoluteTime, actorLabel, relativeTime, shortId } from "@/shared/ui/format";
import { memoryHistoryQueryOptions } from "../queries";

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
    <section className="mt-4 border-t border-border pt-3">
      <button
        type="button"
        onClick={() => setOpen((current) => !current)}
        aria-expanded={open}
        className="label flex items-center gap-1.5 transition-colors hover:text-foreground"
      >
        <Icon
          name={open ? "chevronDown" : "chevronRight"}
          className="size-3.5"
        />
        History chain
      </button>

      {open && (
        <div className="mt-3">
          {history.isPending ? (
            <p className="text-xs text-muted">Walking the chain…</p>
          ) : history.error ? (
            <p className="text-xs text-ember">{history.error.message}</p>
          ) : history.data.chain.length === 0 ? (
            <p className="text-xs text-muted">No history entries.</p>
          ) : (
            <ol className="relative space-y-2 border-l border-border pl-4">
              {history.data.chain.map((entry) => {
                const isCurrent = entry.id === record.id;
                return (
                  <li key={entry.id} className="relative">
                    <span
                      aria-hidden
                      className={
                        isCurrent
                          ? "absolute top-3 -left-[1.30rem] size-2.5 rounded-full bg-accent ring-2 ring-background"
                          : "absolute top-3 -left-[1.30rem] size-2 rounded-full border border-border-strong bg-surface"
                      }
                    />
                    <div
                      className={
                        isCurrent
                          ? "rounded-lg border border-accent/40 bg-accent-soft p-2.5"
                          : "rounded-lg border border-border bg-background/50 p-2.5"
                      }
                    >
                      <div className="flex flex-wrap items-center gap-2">
                        <StatusBadge status={entry.status} />
                        {isCurrent && (
                          <span className="text-[0.7rem] font-medium text-accent">current</span>
                        )}
                        <span className="ml-auto font-mono text-[0.65rem] text-muted">
                          {shortId(entry.id)}
                        </span>
                      </div>
                      <p className="mt-1.5 line-clamp-2 text-xs break-words text-foreground/90">
                        {entry.bodyPreview}
                      </p>
                      <p
                        className="mt-1 text-[0.7rem] text-muted"
                        title={absoluteTime(entry.createdAt)}
                      >
                        {actorLabel(entry.actor.alias, entry.actor.vendor)} · {entry.tome} ·{" "}
                        {relativeTime(entry.createdAt)}
                      </p>
                      {entry.tombstoneReason && (
                        <p className="mt-1.5 rounded border border-ember/35 bg-ember/8 px-2 py-1 text-[0.7rem] text-ember">
                          {entry.tombstoneReason}
                          {entry.tombstonedAt
                            ? ` · ${relativeTime(entry.tombstonedAt)}`
                            : ""}
                        </p>
                      )}
                    </div>
                  </li>
                );
              })}
            </ol>
          )}
        </div>
      )}
    </section>
  );
}
