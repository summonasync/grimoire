"use client";

import { personaSections } from "../lib/persona-document";
import { usePersonaEditor } from "../lib/use-persona-editor";
import { PersonaSectionField } from "./persona-section-field";

export function PersonaEditor() {
  const editor = usePersonaEditor();

  if (editor.loadError !== null) {
    return (
      <div role="alert" className="rounded-lg border border-red-500/40 bg-red-500/10 px-4 py-6 text-sm">
        <p className="font-medium">The persona document could not be loaded.</p>
        <p className="mt-1 text-muted">
          {editor.loadError.code}: {editor.loadError.message}
        </p>
        <button
          type="button"
          onClick={editor.reload}
          className="mt-3 rounded-md border border-border px-3 py-1.5 text-xs transition-colors hover:bg-background"
        >
          Try again
        </button>
      </div>
    );
  }

  if (editor.draft === null) {
    return <p className="text-sm text-muted">Loading persona…</p>;
  }

  const draft = editor.draft;

  return (
    <form noValidate onSubmit={(event) => event.preventDefault()}>
      <div className="flex flex-col gap-4">
        {personaSections.map((spec) => (
          <PersonaSectionField
            key={spec.key}
            spec={spec}
            value={draft[spec.key]}
            disabled={editor.isSaving}
            onChange={editor.setSection}
          />
        ))}
      </div>

      {editor.saveError !== null && (
        <div role="alert" className="mt-4 rounded-md border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm">
          <p className="font-medium">The persona was not saved.</p>
          <p className="mt-0.5 text-muted">
            {editor.saveError.code}: {editor.saveError.message}
          </p>
        </div>
      )}

      <div className="mt-5 flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={editor.save}
          disabled={!editor.isDirty || editor.isSaving}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-foreground transition-opacity disabled:cursor-not-allowed disabled:opacity-50"
        >
          {editor.isSaving ? "Saving…" : "Save persona"}
        </button>
        {editor.isDirty && (
          <button
            type="button"
            onClick={editor.discard}
            className="rounded-md border border-border px-4 py-2 text-sm text-muted transition-colors hover:text-foreground"
          >
            Discard changes
          </button>
        )}
        <span aria-live="polite" className="text-xs text-muted">
          {editor.isSaving ? "Saving…" : editor.isSaved ? "All changes saved" : editor.isDirty ? "Unsaved changes" : ""}
        </span>
      </div>
    </form>
  );
}
