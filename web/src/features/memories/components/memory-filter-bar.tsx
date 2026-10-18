"use client";

import type { MemoryScope, MemoryStatus } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";
import {
  defaultMemoryFilters,
  memoryKinds,
  memoryLimitBounds,
  memoryScopes,
  memoryStatuses,
  toggleStatusFilter,
  type MemoryFilters,
} from "../lib/memory-filters";

interface MemoryFilterBarProps {
  filters: MemoryFilters;
  onChange: (patch: Partial<MemoryFilters>) => void;
  onReset: () => void;
}

const chipBase =
  "rounded-full border px-2.5 py-1 text-xs font-medium transition-colors disabled:cursor-not-allowed";
const chipOff = "border-border text-muted hover:border-accent/50 hover:text-accent";
const chipOn = "border-accent bg-accent text-accent-foreground";

export function MemoryFilterBar({ filters, onChange, onReset }: MemoryFilterBarProps) {
  const toggleScope = (scope: MemoryScope) => {
    const scopes = filters.scopes.includes(scope)
      ? filters.scopes.filter((entry) => entry !== scope)
      : [...filters.scopes, scope];
    onChange({ scopes });
  };

  const statusActive = (status: MemoryStatus) =>
    filters.status === "all" || filters.status.includes(status);

  const limit = Number.parseInt(filters.limit, 10);
  const limitInvalid = filters.limit !== "" && Number.isNaN(limit);

  return (
    <section
      aria-label="Memory filters"
      className="mb-4 space-y-3 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex min-w-56 flex-1 flex-col gap-1">
          <span className="text-xs font-medium text-muted">Search</span>
          <input
            type="search"
            value={filters.query}
            onChange={(event) => onChange({ query: event.target.value })}
            placeholder="Search content…"
            className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted">Kind</span>
          <select
            value={filters.kind}
            onChange={(event) => onChange({ kind: event.target.value as MemoryFilters["kind"] })}
            className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          >
            <option value="">Any kind</option>
            {memoryKinds.map((kind) => (
              <option key={kind} value={kind}>
                {kind}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted">Tags</span>
          <input
            type="text"
            value={filters.tags}
            onChange={(event) => onChange({ tags: event.target.value })}
            placeholder="tag1, tag2"
            className="w-40 rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted">Limit</span>
          <input
            type="number"
            min={memoryLimitBounds.min}
            max={memoryLimitBounds.max}
            value={filters.limit}
            onChange={(event) => onChange({ limit: event.target.value })}
            aria-invalid={limitInvalid}
            className="w-20 rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
        <button
          type="button"
          onClick={onReset}
          className="rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:border-accent/50 hover:text-accent"
        >
          Reset
        </button>
      </div>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <fieldset className="flex flex-wrap items-center gap-1.5">
          <legend className="sr-only">Scopes</legend>
          {memoryScopes.map((scope) => {
            const on = filters.scopes.includes(scope);
            return (
              <button
                key={scope}
                type="button"
                aria-pressed={on}
                onClick={() => toggleScope(scope)}
                className={cn(chipBase, on ? chipOn : chipOff)}
              >
                {scope}
              </button>
            );
          })}
        </fieldset>
        <fieldset className="flex flex-wrap items-center gap-1.5">
          <legend className="sr-only">Statuses</legend>
          {memoryStatuses.map((status) => {
            const on = statusActive(status);
            return (
              <button
                key={status}
                type="button"
                aria-pressed={on}
                onClick={() => onChange({ status: toggleStatusFilter(filters.status, status) })}
                className={cn(chipBase, on ? chipOn : chipOff)}
              >
                {status}
              </button>
            );
          })}
          <button
            type="button"
            aria-pressed={filters.status === "all"}
            onClick={() =>
              onChange({ status: filters.status === "all" ? defaultMemoryFilters().status : "all" })
            }
            className={cn(chipBase, filters.status === "all" ? chipOn : chipOff)}
          >
            all
          </button>
        </fieldset>
      </div>
    </section>
  );
}
