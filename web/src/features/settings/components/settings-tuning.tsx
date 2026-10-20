"use client";

import { useState } from "react";
import { settingsFieldGroups } from "../lib/field-groups";
import { useSettingsForm } from "../lib/use-settings-form";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { Panel } from "@/shared/ui/panel";
import { NotTracked, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { NumericField } from "./numeric-field";
import { PersistenceBanner } from "./persistence-banner";
import { UpdateNotice } from "./update-notice";

const mockupOnlyTabs = [
  {
    label: "Storage",
    reason: "The API reports no collection size, segment breakdown or Qdrant storage metrics.",
  },
  {
    label: "Endpoints",
    reason: "Embedding and Qdrant endpoints are read from the config file and never returned by the API.",
  },
  {
    label: "Advanced",
    reason: "No further tunables are exposed — the fields below are the complete editable surface.",
  },
];

export function SettingsTuning() {
  const form = useSettingsForm();
  const [tab, setTab] = useState<string>(settingsFieldGroups[0]?.id ?? "");

  if (form.loadError !== null) {
    return (
      <QueryFailure
        message={`${form.loadError.code}: ${form.loadError.message}`}
        onRetry={form.reload}
      />
    );
  }

  if (form.draft === null || form.settings === undefined) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-10" />
        <Skeleton className="h-64" />
      </div>
    );
  }

  const draft = form.draft;
  const settings = form.settings;
  const group = settingsFieldGroups.find((entry) => entry.id === tab) ?? settingsFieldGroups[0]!;
  const editableKeys = settings.editable;

  return (
    <div className="space-y-4">
      <PersistenceBanner persistence={settings.persistence} />

      <div className="flex flex-wrap gap-1 border-b border-border pb-2">
        {settingsFieldGroups.map((entry) => (
          <button
            key={entry.id}
            type="button"
            onClick={() => setTab(entry.id)}
            aria-current={tab === entry.id ? "true" : undefined}
            className={cn(
              "rounded-lg px-3 py-1.5 text-sm transition-colors",
              tab === entry.id
                ? "bg-accent-soft font-medium text-foreground ring-1 ring-accent/45"
                : "text-muted hover:bg-foreground/5 hover:text-foreground",
            )}
          >
            {entry.title}
          </button>
        ))}
        <span className="flex items-center gap-1 rounded-lg px-3 py-1.5 text-sm text-muted">
          Serve
          <span className="rounded border border-border px-1 text-[0.6rem] uppercase">ro</span>
        </span>
      </div>

      <Panel title={group.title} description={group.description} icon="settings">
        <div className="grid gap-4 sm:grid-cols-2">
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
        <p className="mt-3 text-[0.7rem] text-muted">
          Editable per API contract: {editableKeys.join(", ")}.
        </p>
      </Panel>

      <Panel
        title="Brief & Context"
        description="Engine options the API reports but PUT does not accept yet."
        icon="book"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="label">Brief max total chars</p>
            <p className="mt-1 text-sm tabular-nums">{settings.memory.brief.maxTotalChars}</p>
          </div>
          <div>
            <p className="label">Brief max entry chars</p>
            <p className="mt-1 text-sm tabular-nums">{settings.memory.brief.maxEntryChars}</p>
          </div>
        </div>
      </Panel>

      <Panel
        title="Serve"
        description="Host options reported by the API — read-only here."
        icon="console"
      >
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <p className="label">Port</p>
            <p className="mt-1 text-sm tabular-nums">{settings.serve.port}</p>
          </div>
          <div>
            <p className="label">GC interval (minutes)</p>
            <p className="mt-1 text-sm tabular-nums">{settings.serve.gcIntervalMinutes}</p>
          </div>
        </div>
      </Panel>

      <Panel
        title="Tabs the design asked for"
        description="Mockup sections with no API backing — shown so the gap stays visible."
        icon="alert"
      >
        <div className="grid gap-2 sm:grid-cols-2">
          {mockupOnlyTabs.map((entry) => (
            <NotTracked key={entry.label} label={entry.label} reason={entry.reason} />
          ))}
        </div>
      </Panel>

      {form.outcome !== null && (
        <UpdateNotice outcome={form.outcome} hint={settings.persistence.hint} />
      )}

      <div className="sticky bottom-0 -mx-1 flex flex-wrap items-center gap-2 rounded-lg border border-border bg-surface/95 px-3 py-2.5 backdrop-blur">
        <Button
          variant="primary"
          size="sm"
          icon="save"
          onClick={form.save}
          disabled={!form.isDirty || !form.isValid || form.isSaving}
        >
          {form.isSaving ? "Sending…" : "Save configuration"}
        </Button>
        {form.isDirty && (
          <Button variant="ghost" size="sm" onClick={form.discard}>
            Discard changes
          </Button>
        )}
        <span aria-live="polite" className="ml-auto text-xs text-muted">
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
