"use client";

import { useState } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { Button } from "@/shared/ui/button";
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

  const reset = () => {
    setConfirming(false);
    setReason("");
  };

  if (confirming) {
    return (
      <div className="w-full space-y-3 rounded-lg border border-jade/40 bg-jade/8 p-3">
        <p className="text-xs text-muted">
          Restoring makes this record active again. If another record superseded it, that one is
          tombstoned — the engine keeps exactly one active truth per fact.
        </p>
        <textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          rows={2}
          autoFocus
          placeholder="Why is this memory being restored?"
          aria-label="Restore reason"
          aria-required
          className="field"
        />
        {restore.error && <p className="text-xs text-ember">{restore.error.message}</p>}
        <div className="flex gap-2">
          <Button
            variant="primary"
            size="sm"
            icon="restore"
            onClick={confirm}
            disabled={trimmedReason.length === 0 || restore.isPending}
          >
            {restore.isPending ? "Restoring…" : "Confirm restore"}
          </Button>
          <Button variant="ghost" size="sm" onClick={reset}>
            Cancel
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {restore.error && <p className="text-xs text-ember">{restore.error.message}</p>}
      <Button variant="outline" size="sm" icon="restore" onClick={() => setConfirming(true)}>
        Restore
      </Button>
    </div>
  );
}
