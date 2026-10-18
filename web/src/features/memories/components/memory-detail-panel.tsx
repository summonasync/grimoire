"use client";

import { useEffect, useState, type ReactNode } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { MemoryEditForm } from "./memory-edit-form";
import { MemoryStatusBadge } from "./memory-status-badge";
import { MemoryTombstoneControl } from "./memory-tombstone-control";

interface MemoryDetailPanelProps {
  record: MemoryRecord;
  onClose: () => void;
}

function MetaItem({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs font-medium text-muted">{label}</dt>
      <dd className="text-sm break-words">{children}</dd>
    </div>
  );
}

function formatOrNull(value: string | null): string {
  return value ? new Date(value).toLocaleString() : "—";
}

export function MemoryDetailPanel({ record, onClose }: MemoryDetailPanelProps) {
  const [editing, setEditing] = useState(false);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        onClose();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-40 flex justify-end">
      <button
        type="button"
        aria-label="Close details"
        onClick={onClose}
        className="absolute inset-0 cursor-default bg-black/40"
      />
      <aside
        role="dialog"
        aria-modal="true"
        aria-label="Memory details"
        className="relative z-10 w-full max-w-xl overflow-y-auto border-l border-border bg-surface p-6 shadow-2xl"
      >
        <div className="mb-4 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h2 className="text-sm font-semibold">Memory</h2>
              <MemoryStatusBadge status={record.status} />
            </div>
            <p className="mt-1 truncate font-mono text-xs text-muted">{record.id}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded-md border border-border px-2 py-1 text-sm text-muted transition-colors hover:text-foreground"
          >
            ✕
          </button>
        </div>

        <p className="rounded-lg border border-border bg-background p-3 text-sm whitespace-pre-wrap">
          {record.content}
        </p>

        <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-3 sm:grid-cols-3">
          <MetaItem label="Scope">{record.scope}</MetaItem>
          <MetaItem label="Kind">{record.kind}</MetaItem>
          <MetaItem label="Actor">
            {record.actor.alias}
            {record.actor.vendor ? ` · ${record.actor.vendor}` : ""}
          </MetaItem>
          <MetaItem label="Importance">{record.importance.toFixed(2)}</MetaItem>
          <MetaItem label="Accesses">{record.accessCount}</MetaItem>
          <MetaItem label="Created">{new Date(record.createdAt).toLocaleString()}</MetaItem>
          <MetaItem label="Last accessed">{formatOrNull(record.lastAccessedAt)}</MetaItem>
          <MetaItem label="Expires">{formatOrNull(record.expiresAt)}</MetaItem>
          <MetaItem label="Project">{record.projectId ?? "—"}</MetaItem>
          <MetaItem label="Session">{record.sessionId ?? "—"}</MetaItem>
          <MetaItem label="Superseded by">{record.supersededBy ?? "—"}</MetaItem>
          <MetaItem label="Tombstoned at">{formatOrNull(record.tombstonedAt)}</MetaItem>
        </dl>

        {record.tombstoneReason && (
          <p className="mt-3 rounded-lg border border-border bg-background p-3 text-xs text-muted">
            Tombstone reason: {record.tombstoneReason}
          </p>
        )}

        {record.tags.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-1.5">
            {record.tags.map((tag) => (
              <span
                key={tag}
                className="rounded-full border border-border px-2 py-0.5 text-xs text-muted"
              >
                #{tag}
              </span>
            ))}
          </div>
        )}

        {record.sources.length > 0 && (
          <div className="mt-3">
            <h3 className="text-xs font-medium text-muted">Sources</h3>
            <ul className="mt-1 list-inside list-disc text-xs break-words text-muted">
              {record.sources.map((source) => (
                <li key={source}>{source}</li>
              ))}
            </ul>
          </div>
        )}

        {record.status !== "tombstoned" && (
          <div className="mt-6 space-y-4 border-t border-border pt-4">
            {editing ? (
              <MemoryEditForm
                record={record}
                onDone={() => setEditing(false)}
                onCancel={() => setEditing(false)}
              />
            ) : (
              record.status === "active" && (
                <button
                  type="button"
                  onClick={() => setEditing(true)}
                  className="rounded-md border border-border px-3 py-2 text-sm transition-colors hover:border-accent/50 hover:text-accent"
                >
                  Edit
                </button>
              )
            )}
            <MemoryTombstoneControl record={record} />
          </div>
        )}
      </aside>
    </div>
  );
}
