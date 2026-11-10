"use client";

import { useId, useState } from "react";
import type { TomeSummary } from "../types";
import { Button } from "@/shared/ui/button";
import { maxTomeDescriptionLength } from "../lib/tome-key";

interface TomeDescriptionFormProps {
  summary: TomeSummary;
  pending: boolean;
  error: string | null;
  onSubmit: (description: string) => void;
  onCancel: () => void;
}

export function TomeDescriptionForm({
  summary,
  pending,
  error,
  onSubmit,
  onCancel,
}: TomeDescriptionFormProps) {
  const inputId = useId();
  const [description, setDescription] = useState(summary.description ?? "");
  const trimmed = description.trim();
  const tooLong = trimmed.length > maxTomeDescriptionLength;
  const valid = trimmed.length > 0 && !tooLong && !pending;

  const submit = () => {
    if (valid) {
      onSubmit(trimmed);
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
        Description of {summary.key}
      </label>
      <textarea
        id={inputId}
        value={description}
        onChange={(event) => setDescription(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            onCancel();
          }
        }}
        rows={2}
        autoFocus
        placeholder="What kind of memories belong in this tome?"
        aria-invalid={error !== null}
        className="field"
      />
      <p className={tooLong ? "text-right text-[0.7rem] text-ember" : "text-right text-[0.7rem] text-muted"}>
        {trimmed.length}/{maxTomeDescriptionLength}
      </p>
      {error && <p className="text-xs text-ember">{error}</p>}
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button variant="primary" size="sm" type="submit" icon="save" disabled={!valid}>
          {pending ? "Saving…" : "Save"}
        </Button>
      </div>
    </form>
  );
}
