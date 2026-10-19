"use client";

import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/shared/ui/page-header";
import { Button } from "@/shared/ui/button";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { Panel } from "@/shared/ui/panel";
import { useTomeFilter } from "@/widgets/app-shell/tome-context";
import { MemoryCreateForm } from "@/features/memories/components/memory-create-form";
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
  const [creating, setCreating] = useState(false);
  const { tome: railTome } = useTomeFilter();

  const deferredQuery = useDeferredValue(filters.query);
  const effectiveFilters =
    filters.query === deferredQuery ? filters : { ...filters, query: deferredQuery };
  const effectiveTome = railTome ?? effectiveFilters.tome;

  const list = useQuery(
    memoriesQueryOptions(toMemoryListParams({ ...effectiveFilters, tome: effectiveTome })),
  );
  const records = list.data?.items ?? [];
  const selected = selectedId ? (records.find((record) => record.id === selectedId) ?? null) : null;

  return (
    <div className="flex h-full min-h-0 flex-col">
      <PageHeader
        title="Memories"
        description="Browse and manage what your agents remember."
        actions={
          <Button
            variant="primary"
            size="sm"
            icon={creating ? "close" : "plus"}
            onClick={() => setCreating((current) => !current)}
          >
            {creating ? "Cancel" : "Add memory"}
          </Button>
        }
      />

      {creating && (
        <Panel title="New memory" icon="plus" className="mb-4">
          <MemoryCreateForm
            defaultTome={effectiveTome || undefined}
            onDone={() => setCreating(false)}
            onCancel={() => setCreating(false)}
          />
        </Panel>
      )}

      <MemoryFilterBar
        filters={filters}
        onChange={(patch) => setFilters((current) => ({ ...current, ...patch }))}
        onReset={() => setFilters(defaultMemoryFilters())}
      />

      <div className="grid min-h-0 flex-1 gap-4 xl:grid-cols-[minmax(0,1fr)_26rem]">
        <section aria-label="Memory list" className="panel min-w-0 overflow-hidden">
          {list.isPending ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2, 3, 4].map((index) => (
                <Skeleton key={index} className="h-14" />
              ))}
            </div>
          ) : list.error ? (
            <div className="p-4">
              <QueryFailure message={list.error.message} onRetry={() => void list.refetch()} />
            </div>
          ) : records.length === 0 ? (
            <div className="p-4">
              <EmptyState
                icon="filter"
                title="Nothing matches these filters"
                reason={
                  effectiveTome
                    ? `No active records in tome '${effectiveTome}' for the current filters — widen the scope or clear the tome filter.`
                    : "The store returned no records for the current filters — try clearing them."
                }
                action={{ label: "Reset filters", onClick: () => setFilters(defaultMemoryFilters()) }}
              />
            </div>
          ) : (
            <>
              <div className="flex items-center justify-between border-b border-border px-4 py-2">
                <p className="text-xs text-muted">
                  {list.data?.count.toLocaleString()} record
                  {list.data?.count === 1 ? "" : "s"} · tome {list.data?.tome}
                </p>
              </div>
              <div className="max-h-[calc(100dvh-19rem)] overflow-y-auto">
                <MemoryList
                  records={records}
                  selectedId={selectedId}
                  onOpen={(id) => setSelectedId(id)}
                />
              </div>
            </>
          )}
        </section>

        {selected ? (
          <MemoryDetailPanel record={selected} onClose={() => setSelectedId(null)} />
        ) : (
          <div className="hidden xl:block">
            <EmptyState
              icon="memories"
              title="Select a record"
              reason="Pick any row to inspect its content, provenance, corrections trail and history chain."
              className="h-full"
            />
          </div>
        )}
      </div>
    </div>
  );
}
