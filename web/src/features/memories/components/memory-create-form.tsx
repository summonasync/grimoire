"use client";

import { useState, type FormEvent } from "react";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icons";
import { memoryKinds, memoryScopes, parseTagList } from "../lib/memory-filters";
import { useCreateMemory } from "../lib/use-memory-mutations";
import type { MemoryKind, MemoryScope } from "@/shared/api/types";

interface MemoryCreateFormProps {
  defaultTome?: string;
  onDone: () => void;
  onCancel: () => void;
}

export function MemoryCreateForm({ defaultTome, onDone, onCancel }: MemoryCreateFormProps) {
  const create = useCreateMemory();
  const [content, setContent] = useState("");
  const [scope, setScope] = useState<MemoryScope>("project");
  const [kind, setKind] = useState<MemoryKind>("fact");
  const [tags, setTags] = useState("");
  const [importance, setImportance] = useState("0.5");
  const [tome, setTome] = useState(defaultTome ?? "");

  const trimmed = content.trim();
  const parsedImportance = Number(importance);
  const canSave =
    trimmed.length > 0 &&
    !create.isPending &&
    importance.trim() !== "" &&
    Number.isFinite(parsedImportance);

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!canSave) {
      return;
    }
    const nextTags = parseTagList(tags);
    const trimmedTome = tome.trim();
    create.mutate(
      {
        content: trimmed,
        scope,
        kind,
        tags: nextTags,
        importance: parsedImportance,
        ...(trimmedTome.length > 0 ? { tome: trimmedTome } : {}),
      },
      { onSuccess: onDone },
    );
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <label className="flex flex-col gap-1">
        <span className="label">Content</span>
        <textarea
          value={content}
          onChange={(event) => setContent(event.target.value)}
          rows={4}
          autoFocus
          placeholder="What should the agents remember?"
          className="field"
        />
      </label>
      <div className="flex flex-wrap gap-3">
        <label className="flex flex-col gap-1">
          <span className="label">Scope</span>
          <select
            value={scope}
            onChange={(event) => setScope(event.target.value as MemoryScope)}
            className="field h-9 py-0 text-sm"
          >
            {memoryScopes.map((entry) => (
              <option key={entry} value={entry}>
                {entry}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="label">Kind</span>
          <select
            value={kind}
            onChange={(event) => setKind(event.target.value as MemoryKind)}
            className="field h-9 py-0 text-sm"
          >
            {memoryKinds.map((entry) => (
              <option key={entry} value={entry}>
                {entry}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="label">Importance</span>
          <input
            type="number"
            min={0}
            max={1}
            step={0.05}
            value={importance}
            onChange={(event) => setImportance(event.target.value)}
            className="field h-9 w-24 py-0 text-sm"
          />
        </label>
        <label className="flex min-w-40 flex-1 flex-col gap-1">
          <span className="label">Tome</span>
          <input
            type="text"
            value={tome}
            onChange={(event) => setTome(event.target.value)}
            placeholder="engine default"
            className="field h-9 py-0 text-sm"
          />
        </label>
      </div>
      <label className="flex flex-col gap-1">
        <span className="label">Tags</span>
        <input
          type="text"
          value={tags}
          onChange={(event) => setTags(event.target.value)}
          placeholder="tag1, tag2"
          className="field"
        />
      </label>
      {create.error && <p className="text-xs text-ember">{create.error.message}</p>}
      <div className="flex gap-2">
        <Button type="submit" variant="primary" size="sm" icon="save" disabled={!canSave}>
          {create.isPending ? "Writing…" : "Write memory"}
        </Button>
        <Button type="button" variant="ghost" size="sm" onClick={onCancel}>
          <Icon name="close" className="size-3.5" />
          Cancel
        </Button>
      </div>
    </form>
  );
}
