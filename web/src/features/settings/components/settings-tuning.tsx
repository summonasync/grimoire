"use client";

import { settingsFieldGroups } from "../lib/field-groups";
import { useSettingsForm } from "../lib/use-settings-form";
import { NumericField } from "./numeric-field";
import { PersistenceBanner } from "./persistence-banner";
import { UpdateNotice } from "./update-notice";

export function SettingsTuning() {
  const form = useSettingsForm();

  if (form.loadError !== null) {
    return (
      <div role="alert" className="rounded-lg border border-red-500/40 bg-red-500/10 px-4 py-6 text-sm">
        <p className="font-medium">The settings could not be loaded.</p>
        <p className="mt-1 text-muted">
          {form.loadError.code}: {form.loadError.message}
        </p>
        <button
          type="button"
          onClick={form.reload}
          className="mt-3 rounded-md border border-border px-3 py-1.5 text-xs transition-colors hover:bg-background"
        >
          Try again
        </button>
      </div>
    );
  }

  if (form.draft === null || form.settings === undefined) {
    return <p className="text-sm text-muted">Loading settings…</p>;
  }

  const draft = form.draft;
  const settings = form.settings;

  return (
    <div className="flex flex-col gap-4">
      {settingsFieldGroups.map((group) => (
        <section key={group.id} className="rounded-lg border border-border bg-surface p-4">
          <h2 className="text-sm font-semibold">{group.title}</h2>
          <p className="mt-0.5 text-xs text-muted">{group.description}</p>
          <div className="mt-3 grid gap-4 sm:grid-cols-2">
            {group.fields.map((field) => (
              <NumericField
                key={field.key}
                spec={field}
                value={draft[field.key]}
                error={form.errors[field.key]}
                disabled={form.isSaving}
                onChange={form.setField}
              />
            ))}
          </div>
        </section>
      ))}

      <section className="rounded-lg border border-border bg-surface p-4">
        <div className="flex items-center gap-2">
          <h2 className="text-sm font-semibold">Serve</h2>
          <span className="rounded-full border border-border px-2 py-0.5 text-xs text-muted">read-only</span>
        </div>
        <p className="mt-0.5 text-xs text-muted">Host options reported by the API; only memory keys are editable.</p>
        <dl className="mt-3 grid gap-4 sm:grid-cols-2">
          <div>
            <dt className="text-sm font-medium">Port</dt>
            <dd className="mt-1 text-sm text-muted">{settings.serve.port}</dd>
          </div>
          <div>
            <dt className="text-sm font-medium">GC interval (minutes)</dt>
            <dd className="mt-1 text-sm text-muted">{settings.serve.gcIntervalMinutes}</dd>
          </div>
        </dl>
      </section>

      <PersistenceBanner persistence={settings.persistence} />
      {form.outcome !== null && <UpdateNotice outcome={form.outcome} hint={settings.persistence.hint} />}

      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={form.save}
          disabled={!form.isDirty || !form.isValid || form.isSaving}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-foreground transition-opacity disabled:cursor-not-allowed disabled:opacity-50"
        >
          {form.isSaving ? "Sending…" : "Save settings"}
        </button>
        {form.isDirty && (
          <button
            type="button"
            onClick={form.discard}
            className="rounded-md border border-border px-4 py-2 text-sm text-muted transition-colors hover:text-foreground"
          >
            Revert edits
          </button>
        )}
        <span aria-live="polite" className="text-xs text-muted">
          {form.isSaving
            ? "Sending…"
            : form.isDirty && !form.isValid
              ? "Fix the highlighted fields"
              : form.isDirty
                ? "Unsaved edits"
                : ""}
        </span>
      </div>
    </div>
  );
}
