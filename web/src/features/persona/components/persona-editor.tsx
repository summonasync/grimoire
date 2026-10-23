"use client";

import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/shared/api/health";
import { statsQueryOptions } from "@/features/stats";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icons";
import { NotTracked, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { personaSections } from "../lib/persona-document";
import { usePersonaEditor } from "../lib/use-persona-editor";
import { PersonaSectionField } from "./persona-section-field";

interface PersonaEditorProps {
  tome: string | null;
}

export function PersonaEditor({ tome }: PersonaEditorProps) {
  const editor = usePersonaEditor(tome);
  const stats = useQuery(statsQueryOptions());
  const health = useQuery(healthQueryOptions());

  if (editor.loadError !== null) {
    return (
      <QueryFailure
        message={`${editor.loadError.code}: ${editor.loadError.message}`}
        onRetry={editor.reload}
      />
    );
  }

  if (editor.draft === null) {
    return (
      <div className="space-y-3">
        {personaSections.map((spec) => (
          <Skeleton key={spec.key} className="h-36" />
        ))}
      </div>
    );
  }

  const draft = editor.draft;
  const filled = personaSections.filter((spec) => draft[spec.key].trim().length > 0);

  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_20rem]">
      <div className="min-w-0">
        <div className="mb-3 flex flex-wrap items-center gap-2">
          <span className="flex items-center gap-2 rounded-lg border border-border-strong bg-surface px-2.5 py-1.5 text-xs">
            <Icon name="book" className="size-3.5 text-muted" />
            <span className="text-muted">{tome ? "Tome" : "Active tome"}</span>
            <span className="font-medium">{tome ?? stats.data?.tome ?? "—"}</span>
          </span>
          <a
            href="#shelf"
            className="flex items-center gap-1.5 rounded-lg border border-border-strong bg-surface px-2.5 py-1.5 text-xs text-muted transition-colors hover:border-accent hover:text-accent"
          >
            <Icon name="arrowLeft" className="size-3.5" />
            Shelf
          </a>
          {health.data && (
            <span
              className={
                health.data.status === "healthy"
                  ? "flex items-center gap-1.5 rounded-lg border border-jade/45 bg-jade/10 px-2.5 py-1.5 text-xs font-medium text-jade"
                  : "flex items-center gap-1.5 rounded-lg border border-ember/45 bg-ember/10 px-2.5 py-1.5 text-xs font-medium text-ember"
              }
            >
              <Icon name="pulse" className="size-3.5" />
              {health.data.status}
            </span>
          )}
        </div>

        <form noValidate onSubmit={(event) => event.preventDefault()} className="space-y-3">
          {personaSections.map((spec, index) => (
            <PersonaSectionField
              key={spec.key}
              spec={spec}
              index={index + 1}
              value={draft[spec.key]}
              disabled={editor.isSaving}
              onChange={editor.setSection}
            />
          ))}

          {editor.saveError !== null && (
            <div role="alert" className="panel border-ember/45 px-4 py-3">
              <p className="text-sm font-medium text-ember">The persona was not saved.</p>
              <p className="mt-0.5 text-xs text-muted">
                {editor.saveError.code}: {editor.saveError.message}
              </p>
            </div>
          )}

          {editor.isDirty && (
            <div className="flex items-start gap-2 rounded-lg border border-gilt/45 bg-gilt/10 px-3 py-2.5">
              <Icon name="alert" className="mt-0.5 size-4 shrink-0 text-gilt" />
              <div className="min-w-0 text-xs">
                <p className="font-medium text-gilt">Unsaved changes</p>
                <p className="text-muted">Your changes are lost if you leave this page.</p>
              </div>
            </div>
          )}

          <div className="sticky bottom-0 -mx-1 flex flex-wrap items-center gap-2 rounded-lg border border-border bg-surface/95 px-3 py-2.5 backdrop-blur">
            <Button
              variant="primary"
              size="sm"
              icon="save"
              onClick={editor.save}
              disabled={!editor.isDirty || editor.isSaving}
            >
              {editor.isSaving ? "Saving…" : "Save changes"}
            </Button>
            {editor.isDirty && (
              <Button variant="ghost" size="sm" onClick={editor.discard}>
                Discard changes
              </Button>
            )}
            {editor.savedTome && (
              <span className="rounded border border-border px-1.5 py-0.5 text-[0.7rem] text-muted">
                tome {editor.savedTome}
              </span>
            )}
            <span aria-live="polite" className="ml-auto text-xs text-muted">
              {editor.isSaving
                ? "Saving…"
                : editor.isSaved
                  ? "All changes saved"
                  : editor.isDirty
                    ? "Unsaved changes"
                    : ""}
            </span>
          </div>
        </form>
      </div>

      <aside className="panel sticky top-4 flex max-h-[calc(100dvh-4rem)] flex-col overflow-hidden">
        <header className="border-b border-border px-4 py-3">
          <h2 className="font-display text-base font-semibold">Persona preview</h2>
          <p className="mt-0.5 text-xs text-muted">
            How the agent reads this tome on startup.
          </p>
        </header>
        <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
          {filled.length === 0 ? (
            <NotTracked
              label="Nothing written yet"
              reason="Every section is empty — agents receive no persona guidance for this tome."
            />
          ) : (
            filled.map((spec) => (
              <section key={spec.key}>
                <p className="label mb-1">{spec.label}</p>
                <p className="text-xs leading-relaxed whitespace-pre-wrap text-foreground/90">
                  {draft[spec.key]}
                </p>
              </section>
            ))
          )}
        </div>
        <footer className="border-t border-border px-4 py-2.5">
          <NotTracked
            label="Test persona"
            reason="The API stores and returns the document only — there is no dry-run endpoint to preview agent behaviour."
          />
        </footer>
      </aside>
    </div>
  );
}
