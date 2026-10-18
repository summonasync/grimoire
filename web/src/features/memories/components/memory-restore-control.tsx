"use client";

import { useState } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { useRestoreMemory } from "../lib/use-memory-mutations";

interface MemoryRestoreControlProps {
  record: MemoryRecord;
}

export function MemoryRestoreControl({ record }: MemoryRestoreControlProps) {
  const restore = useRestoreMemory(record.id);
  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState("");
  const trimmedReason = reason.trim();

  const confirm = () => {
    if (trimmedReason.length === 0 || restore.isPending) {
      return;
    }
    restore.mutate(
      { reason: trimmedReason, tome: record.tome },
      {
        onSuccess: () => {
          setConfirming(false);
          setReason("");
        },
      },
    );
  };

  if (confirming) {
    return (
      <div className="space-y-3 rounded-lg border border-border bg-background p-3">
        <p className="text-xs text-muted">
          Restoring makes this record active again. A superseding record is tombstoned to keep one
          truth per fact.
        </p>
        <textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          rows={2}
          autoFocus
          placeholder="Why is this memory being restored?"
          aria-label="Restore reason"
          className="w-full rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent"
        />
        {restore.error && <p className="text-xs text-red-400">{restore.error.message}</p>}
        <div className="flex gap-2">
          <button
            type="button"
            onClick={confirm}
            disabled={trimmedReason.length === 0 || restore.isPending}
            className="rounded-md bg-emerald-700 px-3 py-2 text-sm font-medium text-white transition-opacity disabled:opacity-50"
          >
            {restore.isPending ? "Restoring…" : "Confirm restore"}
          </button>
          <button
            type="button"
            onClick={() => {
              setConfirming(false);
              setReason("");
            }}
            className="rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:text-foreground"
          >
            Cancel
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {restore.error && <p className="text-xs text-red-400">{restore.error.message}</p>}
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className="rounded-md border border-emerald-700/60 px-3 py-2 text-sm font-medium text-emerald-500 transition-colors hover:bg-emerald-700/10"
      >
        Restore…
      </button>
    </div>
  );
}
