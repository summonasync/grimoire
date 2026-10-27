"use client";

import { scryAttributions, type ScryFilters } from "../lib/scry-filters";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";

interface ScryFilterBarProps {
  filters: ScryFilters;
  onChange: (patch: Partial<ScryFilters>) => void;
  onReset: () => void;
}

function Toggle({
  checked,
  onChange,
  label,
  hint,
}: {
  checked: boolean;
  onChange: (value: boolean) => void;
  label: string;
  hint: string;
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="flex items-center gap-2.5 text-left"
    >
      <span
        className={cn(
          "relative h-5 w-9 shrink-0 rounded-full border transition-colors",
          checked ? "border-jade/60 bg-jade/30" : "border-border-strong bg-background",
        )}
      >
        <span
          className={cn(
            "absolute top-0.5 size-3.5 rounded-full transition-all",
            checked ? "left-[1.15rem] bg-jade" : "left-0.5 bg-muted",
          )}
        />
      </span>
      <span className="min-w-0">
        <span className="block text-xs font-medium">{label}</span>
        <span className="block text-[0.68rem] text-muted">{hint}</span>
      </span>
    </button>
  );
}

export function ScryFilterBar({ filters, onChange, onReset }: ScryFilterBarProps) {
  return (
    <section aria-label="Scry filters" className="panel mb-4 px-4 py-3">
      <div className="flex flex-wrap items-end gap-x-5 gap-y-3">
        <label className="flex min-w-52 flex-col gap-1">
          <span className="label">Tomes</span>
          <input
            type="text"
            value={filters.tomes}
            onChange={(event) => onChange({ tomes: event.target.value })}
            placeholder="all other tomes"
            className="field h-8 py-0 text-xs"
          />
        </label>

        <label className="flex flex-col gap-1">
          <span className="label">Attribution</span>
          <span className="relative">
            <select
              value={filters.attribution}
              onChange={(event) =>
                onChange({ attribution: event.target.value as ScryFilters["attribution"] })
              }
              className="field h-8 appearance-none py-0 pr-7 pl-2.5 text-xs"
            >
              {scryAttributions.map((attribution) => (
                <option key={attribution} value={attribution}>
                  {attribution}
                </option>
              ))}
            </select>
            <Icon
              name="chevronDown"
              className="pointer-events-none absolute top-1/2 right-2 size-3 -translate-y-1/2 text-muted"
            />
          </span>
        </label>

        <Toggle
          checked={filters.includeJournal}
          onChange={(value) => onChange({ includeJournal: value })}
          label="Include journal"
          hint="Search notes & journal entries"
        />

        <button
          type="button"
          onClick={onReset}
          className="ml-auto flex items-center gap-1 rounded-md border border-border px-2 py-1 text-[0.7rem] text-muted transition-colors hover:border-jade/50 hover:text-jade"
        >
          <Icon name="close" className="size-3" />
          Reset
        </button>
      </div>
    </section>
  );
}
