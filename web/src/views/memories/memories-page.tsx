"use client";

import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/shared/ui/page-header";
import { MemoryDetailPanel } from "@/features/memories/components/memory-detail-panel";
import { MemoryFilterBar } from "@/features/memories/components/memory-filter-bar";
import { MemoryList } from "@/features/memories/components/memory-list";
import {
  defaultMemoryFilters,
  toMemoryListParams,
  type MemoryFilters,
} from "@/features/memories/lib/memory-filters";
import { memoriesQueryOptions } from "@/features/memories/queries";

export function MemoriesPage() {
  const [filters, setFilters] = useState<MemoryFilters>(defaultMemoryFilters);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const deferredQuery = useDeferredValue(filters.query);
  const effectiveFilters =
    filters.query === deferredQuery ? filters : { ...filters, query: deferredQuery };

  const list = useQuery(memoriesQueryOptions(toMemoryListParams(effectiveFilters)));
  const records = list.data?.items ?? [];
  const selected = selectedId ? (records.find((record) => record.id === selectedId) ?? null) : null;

  return (
    <>
      <PageHeader
        title="Memories"
        description="Search, inspect, edit and tombstone the memory store."
      />
      <MemoryFilterBar
        filters={filters}
        onChange={(patch) => setFilters((current) => ({ ...current, ...patch }))}
        onReset={() => setFilters(defaultMemoryFilters())}
      />

      {list.isPending ? (
        <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
          Loading memories…
        </p>
      ) : list.error ? (
        <div className="rounded-lg border border-dashed border-border px-6 py-10 text-center">
          <p className="text-sm text-red-400">{list.error.message}</p>
          <button
            type="button"
            onClick={() => void list.refetch()}
            className="mt-3 rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:text-foreground"
          >
            Retry
          </button>
        </div>
      ) : records.length === 0 ? (
        <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
          No memories match the current filters.
        </p>
      ) : (
        <>
          <p className="mb-2 text-xs text-muted">
            {records.length} record{records.length === 1 ? "" : "s"} shown
          </p>
          <MemoryList
            records={records}
            selectedId={selectedId}
            onOpen={(id) => setSelectedId(id)}
          />
        </>
      )}

      {selected && <MemoryDetailPanel record={selected} onClose={() => setSelectedId(null)} />}
    </>
  );
}
