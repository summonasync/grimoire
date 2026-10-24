"use client";

import { useState } from "react";
import type { TomeSummary } from "../types";
import { Button } from "@/shared/ui/button";

interface TomeDeleteConfirmProps {
  summary: TomeSummary;
  pending: boolean;
  error: string | null;
  onDelete: (reason: string) => void;
  onCancel: () => void;
}

export function TomeDeleteConfirm({
  summary,
  pending,
  error,
  onDelete,
  onCancel,
}: TomeDeleteConfirmProps) {
  const [reason, setReason] = useState("");
  const trimmed = reason.trim();

  const confirm = () => {
    if (trimmed.length > 0 && !pending) {
      onDelete(trimmed);
    }
  };

  return (
    <div className="space-y-2">
      <p className="text-xs leading-snug text-muted">
        Tombstones all {summary.active} active records of{" "}
        <span className="font-medium text-foreground">{summary.key}</span>. Nothing is hard-deleted —
        records stay restorable per record until garbage collection ages the tombstones out.
      </p>
      <textarea
        value={reason}
        onChange={(event) => setReason(event.target.value)}
        rows={2}
        autoFocus
        placeholder="Why is this tome being retired?"
        aria-label="Tombstone reason"
        aria-required
        className="field"
      />
      {error && <p className="text-xs text-ember">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          variant="danger"
          size="sm"
          icon="trash"
          onClick={confirm}
          disabled={trimmed.length === 0 || pending}
        >
          {pending ? "Tombstoning…" : "Confirm delete"}
        </Button>
      </div>
    </div>
  );
}
