"use client";

import { useState, type ReactNode } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { Icon, type IconName } from "@/shared/ui/icons";
import { IconButton } from "@/shared/ui/button";
import { KindBadge, ScopeBadge, StatusBadge } from "@/shared/ui/badge";
import { ProjectBadge } from "@/shared/ui/project-badge";
import { absoluteTime, actorLabel, importanceScore, relativeTime, shortId } from "@/shared/ui/format";
import { MemoryEditForm } from "./memory-edit-form";
import { MemoryRestoreControl } from "./memory-restore-control";
import { MemoryTombstoneControl } from "./memory-tombstone-control";
import { MemoryCorrectionsTrail } from "./memory-corrections-trail";
import { MemoryHistorySection } from "./memory-history-section";

interface MemoryDetailPanelProps {
  record: MemoryRecord;
  onClose: () => void;
}

function MetaItem({
  icon,
  label,
  children,
}: {
  icon: IconName;
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="flex min-w-0 items-start gap-2 py-1.5">
      <Icon name={icon} className="mt-0.5 size-3.5 shrink-0 text-muted" />
      <div className="min-w-0">
        <p className="text-[0.68rem] tracking-wide text-muted uppercase">{label}</p>
        <p className="text-xs break-words">{children}</p>
      </div>
    </div>
  );
}

export function MemoryDetailPanel({ record, onClose }: MemoryDetailPanelProps) {
  const [editing, setEditing] = useState(false);
  const dash = "—";

  return (
    <aside
      aria-label="Memory details"
      className="panel sticky top-4 flex max-h-[calc(100dvh-2rem)] min-w-0 flex-col overflow-hidden"
    >
      <header className="flex items-center gap-2 border-b border-border px-4 py-3">
        <KindBadge kind={record.kind} />
        <ProjectBadge projectId={record.projectId} />
        <span className="font-mono text-[0.7rem] text-muted" title={record.id}>
          {shortId(record.id)}
        </span>
        <span className="ml-auto flex items-center gap-1">
          <IconButton icon="close" label="Close details" onClick={onClose} size="sm" />
        </span>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-4 py-3">
        <div className="flex items-start justify-between gap-3">
          <h2 className="line-clamp-2 min-w-0 font-display text-lg leading-snug font-semibold">
            {record.content.split("\n")[0]}
          </h2>
          <StatusBadge status={record.status} className="mt-1" />
        </div>

        <p className="mt-3 rounded-lg border border-border bg-background/60 p-3 text-sm leading-relaxed whitespace-pre-wrap">
          {record.content}
        </p>

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <ScopeBadge scope={record.scope} />
          <span className="flex items-center gap-1 text-xs text-gilt tabular-nums">
            <Icon name="star" className="size-3.5" />
            {importanceScore(record.importance)}
            <span className="text-muted">/10</span>
          </span>
          {record.tags.map((tag) => (
            <span key={tag} className="rounded border border-border px-1.5 text-[0.7rem] text-muted">
              #{tag}
            </span>
          ))}
        </div>

        <div className="mt-3 grid grid-cols-2 gap-x-4 border-t border-border pt-2">
          <MetaItem icon="book" label="Tome">
            {record.tome}
          </MetaItem>
          <MetaItem icon="persona" label="Actor">
            {actorLabel(record.actor.alias, record.actor.vendor)}
          </MetaItem>
          <MetaItem icon="clock" label="Created">
            {absoluteTime(record.createdAt)}
          </MetaItem>
          <MetaItem icon="clock" label="Last accessed">
            {record.lastAccessedAt
              ? `${relativeTime(record.lastAccessedAt)} (${record.accessCount}×)`
              : dash}
          </MetaItem>
          <MetaItem icon="alert" label="Expires">
            {record.expiresAt ? absoluteTime(record.expiresAt) : "never"}
          </MetaItem>
          <MetaItem icon="link" label="Superseded by">
            {record.supersededBy ? (
              <span className="font-mono">{shortId(record.supersededBy)}</span>
            ) : (
              dash
            )}
          </MetaItem>
          <MetaItem icon="console" label="Session">
            {record.sessionId ?? dash}
          </MetaItem>
          <MetaItem icon="memories" label="Body">
            {record.content.length.toLocaleString()} chars
          </MetaItem>
          <MetaItem icon="tombstone" label="Tombstoned">
            {record.tombstonedAt ? absoluteTime(record.tombstonedAt) : dash}
          </MetaItem>
        </div>

        {record.tombstoneReason && (
          <p className="mt-3 rounded-lg border border-ember/40 bg-ember/8 p-3 text-xs text-ember">
            <span className="font-medium">Tombstone reason: </span>
            {record.tombstoneReason}
          </p>
        )}

        {record.sources.length > 0 && (
          <div className="mt-4 border-t border-border pt-3">
            <p className="label mb-2">Sources ({record.sources.length})</p>
            <ul className="space-y-1">
              {record.sources.map((source) => (
                <li key={source} className="flex items-center gap-2 text-xs text-muted">
                  <Icon name="link" className="size-3.5 shrink-0" />
                  <span className="font-mono break-all">{source}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        <div className="mt-4 border-t border-border pt-3">
          {editing ? (
            <MemoryEditForm
              record={record}
              onDone={() => setEditing(false)}
              onCancel={() => setEditing(false)}
            />
          ) : (
            <div className="flex flex-wrap items-center gap-2">
              {record.status === "active" && (
                <button
                  type="button"
                  onClick={() => setEditing(true)}
                  className="inline-flex h-9 items-center gap-2 rounded-lg border border-accent bg-accent px-3 text-sm font-medium text-accent-foreground transition-[filter] hover:brightness-110"
                >
                  <Icon name="edit" className="size-4" />
                  Edit
                </button>
              )}
              {record.status !== "tombstoned" && <MemoryTombstoneControl record={record} />}
              {record.status !== "active" && <MemoryRestoreControl record={record} />}
            </div>
          )}
        </div>

        <MemoryCorrectionsTrail corrections={record.corrections} />
        <MemoryHistorySection record={record} />
      </div>
    </aside>
  );
}
