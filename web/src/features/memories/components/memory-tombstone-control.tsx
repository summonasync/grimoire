"use client";

import { useState } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { useForgetMemory } from "../lib/use-memory-mutations";

interface MemoryTombstoneControlProps {
  record: MemoryRecord;
}

export function MemoryTombstoneControl({ record }: MemoryTombstoneControlProps) {
  const forget = useForgetMemory(record.id);
  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState("");
  const trimmedReason = reason.trim();

  const confirm = () => {
    if (trimmedReason.length === 0 || forget.isPending) {
      return;
    }
    forget.mutate(trimmedReason, {
      onSuccess: () => {
        setConfirming(false);
        setReason("");
      },
    });
  };

  if (confirming) {
    return (
      <div className="space-y-3 rounded-lg border border-border bg-background p-3">
        <p className="text-xs text-muted">
          Tombstoning hides the record from recall. The reason is stored on the tombstone.
        </p>
        <textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          rows={2}
          autoFocus
          placeholder="Why is this memory being retired?"
          aria-label="Tombstone reason"
          className="w-full rounded-md border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-accent"
        />
        {forget.error && <p className="text-xs text-red-400">{forget.error.message}</p>}
        <div className="flex gap-2">
          <button
            type="button"
            onClick={confirm}
            disabled={trimmedReason.length === 0 || forget.isPending}
            className="rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white transition-opacity disabled:opacity-50"
          >
            {forget.isPending ? "Tombstoning…" : "Confirm tombstone"}
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
      {forget.error && <p className="text-xs text-red-400">{forget.error.message}</p>}
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className="rounded-md border border-red-700/60 px-3 py-2 text-sm font-medium text-red-400 transition-colors hover:bg-red-700/10"
      >
        Tombstone…
      </button>
    </div>
  );
}
