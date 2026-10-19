"use client";

import { useState } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { Button } from "@/shared/ui/button";
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

  const reset = () => {
    setConfirming(false);
    setReason("");
  };

  if (confirming) {
    return (
      <div className="w-full space-y-3 rounded-lg border border-ember/40 bg-ember/8 p-3">
        <p className="text-xs text-muted">
          Tombstoning hides the record from recall. Nothing is deleted — the reason is stored on the
          tombstone until garbage collection owns the hard delete.
        </p>
        <textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          rows={2}
          autoFocus
          placeholder="Why is this memory being retired?"
          aria-label="Tombstone reason"
          aria-required
          className="field"
        />
        {forget.error && <p className="text-xs text-ember">{forget.error.message}</p>}
        <div className="flex gap-2">
          <Button
            variant="danger"
            size="sm"
            icon="tombstone"
            onClick={confirm}
            disabled={trimmedReason.length === 0 || forget.isPending}
          >
            {forget.isPending ? "Tombstoning…" : "Confirm tombstone"}
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
      {forget.error && <p className="text-xs text-ember">{forget.error.message}</p>}
      <Button variant="outline" size="sm" icon="tombstone" onClick={() => setConfirming(true)}>
        Tombstone
      </Button>
    </div>
  );
}
