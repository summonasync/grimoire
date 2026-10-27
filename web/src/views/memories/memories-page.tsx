"use client";

import { useEffect, useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/shared/ui/page-header";
import { Button } from "@/shared/ui/button";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { Icon } from "@/shared/ui/icons";
import { Panel } from "@/shared/ui/panel";
import { useTomeFilter } from "@/widgets/app-shell/tome-context";
import { clearHandedOffRecord, readHandedOffRecord } from "@/widgets/app-shell/record-handoff";
import { RecordMoveBar } from "@/features/shelf";
import { MemoryCreateForm } from "@/features/memories/components/memory-create-form";
import { MemoryDetailPanel } from "@/features/memories/components/memory-detail-panel";
import { MemoryFilterBar } from "@/features/memories/components/memory-filter-bar";
import { MemoryList } from "@/features/memories/components/memory-list";
import {
  defaultMemoryFilters,
  toMemoryListParams,
  type MemoryFilters,
} from "@/features/memories/lib/memory-filters";
import { memoriesQueryOptions, memoryQueryOptions } from "@/features/memories/queries";

export function MemoriesPage() {
  const [filters, setFilters] = useState<MemoryFilters>(defaultMemoryFilters);
  const [selectedId, setSelectedId] = useState<string | null>(readHandedOffRecord);

  useEffect(() => {
    clearHandedOffRecord();
  }, []);
  const [checkedIds, setCheckedIds] = useState<ReadonlySet<string>>(new Set());
  const [creating, setCreating] = useState(false);
  const { tome: railTome } = useTomeFilter();

  const toggleChecked = (id: string) => {
    setCheckedIds((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const deferredQuery = useDeferredValue(filters.query);
  const effectiveFilters =
    filters.query === deferredQuery ? filters : { ...filters, query: deferredQuery };
  const effectiveTome = railTome ?? effectiveFilters.tome;

  const list = useQuery(
    memoriesQueryOptions(toMemoryListParams({ ...effectiveFilters, tome: effectiveTome })),
  );
  const records = list.data?.items ?? [];
  const listed = selectedId ? (records.find((record) => record.id === selectedId) ?? null) : null;
  const offList = useQuery({
    ...memoryQueryOptions(selectedId ?? ""),
    enabled: selectedId !== null && listed === null,
  });
  const selected = listed ?? (selectedId ? (offList.data ?? null) : null);

  return (
    <div className="flex h-full min-h-0 flex-col">
      <PageHeader
        title="Memories"
        description="Browse and manage what your agents remember."
        actions={
          <>
            <span className="flex items-center gap-1.5 rounded-lg border border-border-strong bg-surface/80 px-2.5 py-1.5 text-xs">
              <Icon name="book" className="size-3.5 text-muted" />
              <span className="text-muted">Tome</span>
              <span className="font-medium">{effectiveTome ?? list.data?.tome ?? "—"}</span>
            </span>
            <a
              href="#shelf"
              className="flex items-center gap-1.5 rounded-lg border border-border-strong bg-surface/80 px-2.5 py-1.5 text-xs text-muted transition-colors hover:border-jade hover:text-jade"
            >
              <Icon name="arrowLeft" className="size-3.5" />
              Shelf
            </a>
            <Button
              variant="primary"
              size="sm"
              icon={creating ? "close" : "plus"}
              onClick={() => setCreating((current) => !current)}
            >
              {creating ? "Cancel" : "Add memory"}
            </Button>
          </>
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
              <RecordMoveBar
                ids={[...checkedIds]}
                sourceTome={effectiveTome}
                onDone={() => setCheckedIds(new Set())}
              />
              <div className="max-h-[calc(100dvh-19rem)] overflow-y-auto">
                <MemoryList
                  records={records}
                  selectedId={selectedId}
                  onOpen={(id) => setSelectedId(id)}
                  selection={{ ids: checkedIds, onToggle: toggleChecked }}
                />
              </div>
            </>
          )}
        </section>

        {selected ? (
          <MemoryDetailPanel
            record={selected}
            onClose={() => setSelectedId(null)}
            onOpenRecord={(id) => setSelectedId(id)}
          />
        ) : selectedId && offList.error ? (
          <aside aria-label="Memory details" className="panel p-4">
            <QueryFailure message={offList.error.message} onRetry={() => void offList.refetch()} />
          </aside>
        ) : selectedId ? (
          <aside aria-label="Memory details" className="panel space-y-2 p-4">
            <Skeleton className="h-14" />
            <Skeleton className="h-24" />
            <Skeleton className="h-36" />
          </aside>
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
