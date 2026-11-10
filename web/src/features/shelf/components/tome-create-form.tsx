"use client";

import { useId, useState } from "react";
import { Button } from "@/shared/ui/button";
import { maxTomeDescriptionLength, maxTomeKeyLength, normalizeTomeKey } from "../lib/tome-key";
import { useShelfToasts } from "../lib/shelf-toasts";
import { useCreateTomeManifest } from "../lib/use-shelf-mutations";

interface TomeCreateFormProps {
  onDone: () => void;
  onCancel: () => void;
}

export function TomeCreateForm({ onDone, onCancel }: TomeCreateFormProps) {
  const inputId = useId();
  const descriptionId = useId();
  const create = useCreateTomeManifest();
  const { push } = useShelfToasts();
  const [key, setKey] = useState("");
  const [description, setDescription] = useState("");

  const normalized = normalizeTomeKey(key);
  const trimmedDescription = description.trim();
  const descriptionTooLong = trimmedDescription.length > maxTomeDescriptionLength;
  const canCreate =
    normalized !== null && trimmedDescription.length > 0 && !descriptionTooLong && !create.isPending;

  const submit = () => {
    if (normalized === null || trimmedDescription.length === 0 || descriptionTooLong) {
      return;
    }
    create.mutate(
      { key: normalized, description: trimmedDescription },
      {
        onSuccess: (manifest) => {
          push({
            kind: "success",
            text: `Tome '${manifest.key}' created — agents pick it by its description when routing memories.`,
          });
          onDone();
        },
        onError: (error) => push({ kind: "error", text: error.message }),
      },
    );
  };

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        submit();
      }}
      className="space-y-3"
    >
      <label className="flex flex-col gap-1" htmlFor={inputId}>
        <span className="label">Key</span>
        <input
          id={inputId}
          value={key}
          onChange={(event) => setKey(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              onCancel();
            }
          }}
          autoFocus
          placeholder="project-notes"
          aria-invalid={key.trim().length > 0 && normalized === null}
          className="field font-mono text-sm"
        />
      </label>
      {key.trim().length > 0 &&
        (normalized === null ? (
          <p className="text-xs text-ember">
            Keys are lowercase kebab-case: letters, digits and dashes, up to {maxTomeKeyLength}{" "}
            characters.
          </p>
        ) : normalized !== key.trim() ? (
          <p className="text-xs text-muted">
            Will be created as <span className="font-mono">{normalized}</span>.
          </p>
        ) : null)}
      <label className="flex flex-col gap-1" htmlFor={descriptionId}>
        <span className="label">Description</span>
        <textarea
          id={descriptionId}
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Escape") {
              onCancel();
            }
          }}
          rows={2}
          placeholder="What kind of memories belong in this tome?"
          className="field"
        />
      </label>
      <p
        className={
          descriptionTooLong ? "text-right text-[0.7rem] text-ember" : "text-right text-[0.7rem] text-muted"
        }
      >
        {trimmedDescription.length}/{maxTomeDescriptionLength}
      </p>
      {create.error && <p className="text-xs text-ember">{create.error.message}</p>}
      <div className="flex justify-end gap-2">
        <Button variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
        <Button variant="primary" size="sm" type="submit" icon="plus" disabled={!canCreate}>
          {create.isPending ? "Creating…" : "Create tome"}
        </Button>
      </div>
    </form>
  );
}
