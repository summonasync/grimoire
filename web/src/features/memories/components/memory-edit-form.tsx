"use client";

import { useState, type FormEvent } from "react";
import type { MemoryRecord } from "@/shared/api/types";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icons";
import { parseTagList } from "../lib/memory-filters";
import { usePatchMemory } from "../lib/use-memory-mutations";

interface MemoryEditFormProps {
  record: MemoryRecord;
  onDone: () => void;
  onCancel: () => void;
}

export function MemoryEditForm({ record, onDone, onCancel }: MemoryEditFormProps) {
  const patch = usePatchMemory(record.id);
  const [content, setContent] = useState(record.content);
  const [tags, setTags] = useState(record.tags.join(", "));
  const [importance, setImportance] = useState(String(record.importance));
  const [reason, setReason] = useState("");

  const nextTags = parseTagList(tags);
  const nextContent = content.trim();
  const trimmedReason = reason.trim();
  const parsedImportance = Number(importance);
  const contentChanged = nextContent.length > 0 && nextContent !== record.content;
  const tagsChanged =
    nextTags.length !== record.tags.length ||
    nextTags.some((tag, index) => tag !== record.tags[index]);
  const importanceChanged =
    importance.trim() !== "" &&
    Number.isFinite(parsedImportance) &&
    parsedImportance !== record.importance;
  const dirty = contentChanged || tagsChanged || importanceChanged;
  const reasonMissing = trimmedReason.length === 0;
  const canSave = dirty && !reasonMissing && !patch.isPending;

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!canSave) {
      return;
    }
    patch.mutate(
      {
        reason: trimmedReason,
        ...(contentChanged ? { content: nextContent } : {}),
        ...(tagsChanged ? { tags: nextTags } : {}),
        ...(importanceChanged ? { importance: parsedImportance } : {}),
      },
      { onSuccess: onDone },
    );
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <label className="flex flex-col gap-1">
        <span className="label">Content</span>
        <textarea value={content} onChange={(event) => setContent(event.target.value)} rows={5} className="field" />
      </label>
      <div className="flex gap-3">
        <label className="flex flex-1 flex-col gap-1">
          <span className="label">Tags</span>
          <input
            type="text"
            value={tags}
            onChange={(event) => setTags(event.target.value)}
            placeholder="tag1, tag2"
            className="field"
          />
        </label>
        <label className="flex w-28 flex-col gap-1">
          <span className="label">Importance</span>
          <input
            type="number"
            min={0}
            max={1}
            step={0.05}
            value={importance}
            onChange={(event) => setImportance(event.target.value)}
            className="field"
          />
        </label>
      </div>
      <label className="flex flex-col gap-1">
        <span className="label flex items-center gap-1">
          Reason <span className="text-accent">*</span>
        </span>
        <textarea
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          rows={2}
          placeholder="Why is this change needed?"
          aria-label="Correction reason"
          aria-required
          className="field"
        />
        <span className="text-[0.7rem] text-muted">
          {reasonMissing
            ? "A reason is required — it is written to the correction trail."
            : "Stored on the correction trail."}
        </span>
      </label>
      {patch.error && <p className="text-xs text-ember">{patch.error.message}</p>}
      <div className="flex gap-2">
        <Button
          type="submit"
          variant="primary"
          size="sm"
          icon="save"
          disabled={!canSave}
          title={reasonMissing ? "A reason is required to save." : undefined}
        >
          {patch.isPending ? "Saving…" : "Save"}
        </Button>
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          <Icon name="close" className="size-3.5" />
          Cancel
        </Button>
      </div>
    </form>
  );
}
