"use client";

import { useId, useState } from "react";
import type { TomeSummary } from "../types";
import { Button } from "@/shared/ui/button";

interface TomeRenameFormProps {
  summary: TomeSummary;
  pending: boolean;
  error: string | null;
  onSubmit: (to: string) => void;
  onCancel: () => void;
}

export function TomeRenameForm({ summary, pending, error, onSubmit, onCancel }: TomeRenameFormProps) {
  const inputId = useId();
  const [name, setName] = useState("");
  const next = name.trim();
  const valid = next.length > 0 && next !== summary.key;

  const submit = () => {
    if (valid && !pending) {
      onSubmit(next);
    }
  };

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
      className="space-y-2"
    >
      <label className="label" htmlFor={inputId}>
        Rename tome
      </label>
      <input
        id={inputId}
        value={name}
        onChange={(event) => setName(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            onCancel();
          }
        }}
        autoFocus
        placeholder={summary.key}
        aria-invalid={error !== null}
        className="field font-mono text-sm"
      />
      {error && <p className="text-xs text-ember">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button variant="primary" size="sm" type="submit" icon="save" disabled={!valid || pending}>
          {pending ? "Renaming…" : "Rename"}
        </Button>
      </div>
    </form>
  );
}
