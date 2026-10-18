"use client";

import { scryAttributions, type ScryFilters } from "../lib/scry-filters";

interface ScryFilterBarProps {
  filters: ScryFilters;
  onChange: (patch: Partial<ScryFilters>) => void;
  onReset: () => void;
}

export function ScryFilterBar({ filters, onChange, onReset }: ScryFilterBarProps) {
  return (
    <section
      aria-label="Scry filters"
      className="mb-4 rounded-lg border border-border bg-surface p-4"
    >
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex min-w-64 flex-1 flex-col gap-1">
          <span className="text-xs font-medium text-muted">Query</span>
          <input
            type="search"
            value={filters.query}
            onChange={(event) => onChange({ query: event.target.value })}
            placeholder="Search other tomes…"
            className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted">Attribution</span>
          <select
            value={filters.attribution}
            onChange={(event) =>
              onChange({ attribution: event.target.value as ScryFilters["attribution"] })
            }
            className="rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          >
            {scryAttributions.map((attribution) => (
              <option key={attribution} value={attribution}>
                {attribution}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1">
          <span className="text-xs font-medium text-muted">Tomes</span>
          <input
            type="text"
            value={filters.tomes}
            onChange={(event) => onChange({ tomes: event.target.value })}
            placeholder="tome-a, tome-b"
            className="w-44 rounded-md border border-border bg-background px-3 py-2 text-sm outline-none focus:border-accent"
          />
        </label>
        <label className="flex items-center gap-2 pb-2 text-sm">
          <input
            type="checkbox"
            checked={filters.includeJournal}
            onChange={(event) => onChange({ includeJournal: event.target.checked })}
            className="size-4 accent-accent"
          />
          <span className="text-muted">Include journal</span>
        </label>
        <button
          type="button"
          onClick={onReset}
          className="rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:border-accent/50 hover:text-accent"
        >
          Reset
        </button>
      </div>
    </section>
  );
}
