"use client";

import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/shared/ui/page-header";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icons";
import { elapsedTime } from "@/shared/ui/format";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import {
  ScryFilterBar,
  ScryResultItem,
  defaultScryFilters,
  toScryParams,
  type ScryFilters,
} from "@/features/scry";
import { scryQueryOptions } from "@/features/scry/queries";

export function ScryPage() {
  const [filters, setFilters] = useState<ScryFilters>(defaultScryFilters);
  const deferredQuery = useDeferredValue(filters.query);
  const effectiveFilters =
    filters.query === deferredQuery ? filters : { ...filters, query: deferredQuery };
  const params = toScryParams(effectiveFilters);
  const hasQuery = params.query.length > 0;

  const search = useQuery({ ...scryQueryOptions(params), enabled: hasQuery });
  const result = hasQuery ? search.data : undefined;

  return (
    <div className="space-y-4">
      <PageHeader title="Scry" description="Search across tomes. See what others remember." />

      <div className="panel flex items-center gap-3 px-4 py-3">
        <Icon name="search" className="size-5 shrink-0 text-jade" />
        <input
          type="search"
          value={filters.query}
          onChange={(event) => setFilters((current) => ({ ...current, query: event.target.value }))}
          placeholder="What do the other tomes know about…"
          aria-label="Scry query"
          className="min-w-0 flex-1 rounded-md bg-transparent text-base outline-none placeholder:text-muted focus-visible:ring-2 focus-visible:ring-jade/60"
        />
        {filters.query.length > 0 && (
          <Button
            variant="ghost"
            size="sm"
            icon="close"
            onClick={() => setFilters((current) => ({ ...current, query: "" }))}
          >
            Clear
          </Button>
        )}
      </div>

      <ScryFilterBar
        filters={filters}
        onChange={(patch) => setFilters((current) => ({ ...current, ...patch }))}
        onReset={() => setFilters(defaultScryFilters())}
      />

      {!hasQuery ? (
        <EmptyState
          icon="scry"
          title="Nothing scryed yet"
          reason="Type a query above to read across the other tomes. Attribution controls how much actor detail you get back."
        />
      ) : search.isPending ? (
        <div className="space-y-2">
          {[0, 1, 2].map((index) => (
            <Skeleton key={index} className="h-20" />
          ))}
        </div>
      ) : search.error ? (
        <QueryFailure message={search.error.message} onRetry={() => void search.refetch()} />
      ) : (
        result &&
        (result.tomes.length === 0 ? (
          <EmptyState
            icon="book"
            title="No other tomes exist"
            reason="Scry reads tomes other than your own — there is nothing else to read yet."
          />
        ) : result.items.length === 0 ? (
          <EmptyState
            icon="search"
            title="No matches"
            reason={`Nothing in ${result.tomes.join(", ")} answered this query. Try fewer terms or include the journal.`}
          />
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <p className="font-display text-lg font-semibold">
                {result.count} result{result.count === 1 ? "" : "s"}
              </p>
              <p className="text-xs text-muted">
                across {result.tomes.join(", ")} · attribution {result.attribution}
              </p>
              {result.elapsedMs >= 0 && (
                <p className="text-xs text-muted tabular-nums">
                  answered in {elapsedTime(result.elapsedMs)}
                </p>
              )}
            </div>
            <ul className="space-y-2">
              {result.items.map((item) => (
                <ScryResultItem key={item.memoryId} item={item} />
              ))}
            </ul>
          </>
        ))
      )}
    </div>
  );
}
