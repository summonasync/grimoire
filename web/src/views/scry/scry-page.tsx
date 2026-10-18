"use client";

import { useDeferredValue, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader } from "@/shared/ui/page-header";
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
    <>
      <PageHeader
        title="Scry"
        description="Recall facts from other tomes, anonymized per the attribution level."
      />
      <ScryFilterBar
        filters={filters}
        onChange={(patch) => setFilters((current) => ({ ...current, ...patch }))}
        onReset={() => setFilters(defaultScryFilters())}
      />

      {!hasQuery ? (
        <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
          Type a query to scry the other tomes.
        </p>
      ) : search.isPending ? (
        <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
          Scrying…
        </p>
      ) : search.error ? (
        <div className="rounded-lg border border-dashed border-border px-6 py-10 text-center">
          <p className="text-sm text-red-400">{search.error.message}</p>
          <button
            type="button"
            onClick={() => void search.refetch()}
            className="mt-3 rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:text-foreground"
          >
            Retry
          </button>
        </div>
      ) : (
        result && (
          <>
            {result.tomes.length === 0 ? (
              <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
                No other tomes exist — there is nothing to scry.
              </p>
            ) : result.items.length === 0 ? (
              <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
                No matches in {result.tomes.join(", ")}.
              </p>
            ) : (
              <>
                <p className="mb-2 text-xs text-muted">
                  {result.count} match{result.count === 1 ? "" : "es"} in {result.tomes.join(", ")}{" "}
                  · attribution {result.attribution}
                </p>
                <ul className="space-y-2">
                  {result.items.map((item) => (
                    <ScryResultItem key={item.memoryId} item={item} />
                  ))}
                </ul>
              </>
            )}
          </>
        )
      )}
    </>
  );
}
