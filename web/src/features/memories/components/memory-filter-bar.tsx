"use client";

import type { MemoryScope, MemoryStatus } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";
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

function Select({
  label,
  value,
  onChange,
  children,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  children: React.ReactNode;
}) {
  return (
    <label className="flex items-center gap-1.5 text-xs text-muted">
      <span className="whitespace-nowrap">{label}</span>
      <span className="relative">
        <select
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className="field h-8 appearance-none py-0 pr-7 pl-2.5 text-xs"
        >
          {children}
        </select>
        <Icon
          name="chevronDown"
          className="pointer-events-none absolute top-1/2 right-2 size-3 -translate-y-1/2 text-muted"
        />
      </span>
    </label>
  );
}

function StatusChip({
  label,
  on,
  onClick,
}: {
  label: string;
  on: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      aria-pressed={on}
      onClick={onClick}
      className={cn(
        "rounded-md border px-2 py-1 text-[0.7rem] font-medium transition-colors",
        on
          ? "border-jade/55 bg-jade/10 text-foreground"
          : "border-border text-muted hover:border-jade/40 hover:text-foreground",
      )}
    >
      {label}
    </button>
  );
}

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
      className="panel mb-4 flex flex-wrap items-center gap-x-4 gap-y-2 px-3 py-2.5"
    >
      <label className="relative flex min-w-52 flex-1 items-center">
        <Icon name="search" className="pointer-events-none absolute left-2.5 size-4 text-muted" />
        <input
          type="search"
          value={filters.query}
          onChange={(event) => onChange({ query: event.target.value })}
          placeholder="Search content…"
          aria-label="Search content"
          className="field h-9 pl-9"
        />
      </label>

      <fieldset className="flex items-center gap-1.5">
        <legend className="sr-only">Scopes</legend>
        <span className="text-[0.68rem] tracking-wide text-muted uppercase">Scope</span>
        {memoryScopes.map((scope) => {
          const on = filters.scopes.includes(scope);
          return (
            <button
              key={scope}
              type="button"
              aria-pressed={on}
              onClick={() => toggleScope(scope)}
              className={cn(
                "rounded-md border px-2 py-1 text-[0.7rem] font-medium transition-colors",
                on
                  ? "border-jade/55 bg-jade/10 text-foreground"
                  : "border-border text-muted hover:border-jade/40 hover:text-foreground",
              )}
            >
              {scope}
            </button>
          );
        })}
      </fieldset>

      <Select
        label="Kind"
        value={filters.kind}
        onChange={(value) => onChange({ kind: value as MemoryFilters["kind"] })}
      >
        <option value="">All</option>
        {memoryKinds.map((kind) => (
          <option key={kind} value={kind}>
            {kind}
          </option>
        ))}
      </Select>

      <label className="flex items-center gap-1.5 text-xs text-muted">
        <span className="whitespace-nowrap">Tags</span>
        <input
          type="text"
          value={filters.tags}
          onChange={(event) => onChange({ tags: event.target.value })}
          placeholder="tag1, tag2"
          aria-label="Tags"
          className="field h-8 w-32 text-xs"
        />
      </label>

      <label className="flex items-center gap-1.5 text-xs text-muted">
        <span className="whitespace-nowrap">Limit</span>
        <input
          type="number"
          min={memoryLimitBounds.min}
          max={memoryLimitBounds.max}
          value={filters.limit}
          onChange={(event) => onChange({ limit: event.target.value })}
          aria-invalid={limitInvalid}
          aria-label="Result limit"
          className="field h-8 w-16 text-xs"
        />
      </label>

      <div className="flex items-center gap-1.5">
        {memoryStatuses.map((status) => (
          <StatusChip
            key={status}
            label={status}
            on={statusActive(status)}
            onClick={() => onChange({ status: toggleStatusFilter(filters.status, status) })}
          />
        ))}
        <StatusChip
          label="all"
          on={filters.status === "all"}
          onClick={() =>
            onChange({
              status: filters.status === "all" ? defaultMemoryFilters().status : "all",
            })
          }
        />
      </div>

      <button
        type="button"
        onClick={onReset}
        className="flex items-center gap-1 rounded-md border border-border px-2 py-1 text-[0.7rem] text-muted transition-colors hover:border-jade/50 hover:text-jade"
      >
        <Icon name="close" className="size-3" />
        Reset
      </button>
    </section>
  );
}
