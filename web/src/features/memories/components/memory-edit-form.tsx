"use client";

import { useState, type FormEvent } from "react";
import type { MemoryRecord } from "@/shared/api/types";
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

  const nextTags = parseTagList(tags);
  const nextContent = content.trim();
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

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!dirty || patch.isPending) {
      return;
    }
    patch.mutate(
      {
        ...(contentChanged ? { content: nextContent } : {}),
        ...(tagsChanged ? { tags: nextTags } : {}),
        ...(importanceChanged ? { importance: parsedImportance } : {}),
      },
      { onSuccess: onDone },
    );
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3 border-t border-border pt-4">
      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-muted">Content</span>
        <textarea
          value={content}
          onChange={(event) => setContent(event.target.value)}
          rows={5}
          className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
        />
      </label>
      <div className="flex gap-3">
        <label className="flex flex-1 flex-col gap-1">
          <span className="text-xs font-medium text-muted">Tags</span>
          <input
            type="text"
            value={tags}
            onChange={(event) => setTags(event.target.value)}
            placeholder="tag1, tag2"
            className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
        <label className="flex w-28 flex-col gap-1">
          <span className="text-xs font-medium text-muted">Importance</span>
          <input
            type="number"
            min={0}
            max={1}
            step={0.05}
            value={importance}
            onChange={(event) => setImportance(event.target.value)}
            className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
      </div>
      {patch.error && <p className="text-xs text-red-400">{patch.error.message}</p>}
      <div className="flex gap-2">
        <button
          type="submit"
          disabled={!dirty || patch.isPending}
          className="rounded-md bg-accent px-3 py-2 text-sm font-medium text-accent-foreground transition-opacity disabled:opacity-50"
        >
          {patch.isPending ? "Saving…" : "Save"}
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:text-foreground"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
