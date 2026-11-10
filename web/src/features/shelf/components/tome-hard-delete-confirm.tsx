"use client";

import { useId, useState } from "react";
import type { TomeSummary } from "../types";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icons";
import { compactNumber } from "@/shared/ui/format";

interface TomeHardDeleteConfirmProps {
  summary: TomeSummary;
  pending: boolean;
  error: string | null;
  onConfirm: (confirm: string) => void;
  onCancel: () => void;
}

export function TomeHardDeleteConfirm({
  summary,
  pending,
  error,
  onConfirm,
  onCancel,
}: TomeHardDeleteConfirmProps) {
  const inputId = useId();
  const [typed, setTyped] = useState("");
  const matched = typed === summary.key;

  const confirm = () => {
    if (matched && !pending) {
      onConfirm(typed);
    }
  };

  return (
    <div className="space-y-2">
      <p className="flex items-start gap-1.5 text-xs leading-snug text-ember">
        <Icon name="alert" className="mt-0.5 size-3.5 shrink-0" />
        <span>
          Hard delete wipes every record of{" "}
          <span className="font-medium">{summary.key}</span> — active, tombstoned and superseded.
          Nothing is kept, nothing can be restored, and there is no undo.
        </span>
      </p>
      <p className="text-xs leading-snug text-muted">
        {compactNumber(summary.active)} active and {compactNumber(summary.tombstoned)} tombstoned
        records will be destroyed
        {summary.manifest
          ? ", and the manifest with its description is deleted as well."
          : "."}
      </p>
      <label className="flex flex-col gap-1" htmlFor={inputId}>
        <span className="label">
          Type <span className="font-mono normal-case">{summary.key}</span> to confirm
        </span>
        <input
          id={inputId}
          value={typed}
          onChange={(event) => setTyped(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              onCancel();
            }
          }}
          autoFocus
          placeholder={summary.key}
          autoComplete="off"
          spellCheck={false}
          className="field font-mono text-sm"
        />
      </label>
      {typed.length > 0 && !matched && (
        <p className="text-[0.7rem] text-muted">
          Keep typing — the key must match exactly, including case.
        </p>
      )}
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
          disabled={!matched || pending}
        >
          {pending ? "Purging…" : "Delete forever"}
        </Button>
      </div>
    </div>
  );
}
