"use client";

import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/shared/api/health";
import { PageHeader } from "@/shared/ui/page-header";
import { ProviderHealthCard } from "@/features/stats/components/provider-health-card";
import { StatsTile } from "@/features/stats/components/stats-tile";
import { statsQueryOptions } from "@/features/stats/queries";

function QueryError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="rounded-lg border border-dashed border-border px-6 py-8 text-center">
      <p className="text-sm text-red-400">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 rounded-md border border-border px-3 py-2 text-sm text-muted transition-colors hover:text-foreground"
      >
        Retry
      </button>
    </div>
  );
}

export function StatsPage() {
  const stats = useQuery(statsQueryOptions());
  const health = useQuery(healthQueryOptions());
  const scopes = stats.data ? Object.entries(stats.data.activeByScope) : [];
  const tomeRows = stats.data ? Object.entries(stats.data.byTome) : [];

  return (
    <>
      <PageHeader
        title="Stats"
        description="Store counts, scope distribution and pending compaction."
      />

      {stats.isPending ? (
        <p className="rounded-lg border border-dashed border-border px-6 py-10 text-center text-sm text-muted">
          Loading stats…
        </p>
      ) : stats.error ? (
        <QueryError message={stats.error.message} onRetry={() => void stats.refetch()} />
      ) : (
        stats.data && (
          <div className="space-y-4">
            <p className="text-xs text-muted">Resolved tome: {stats.data.tome}</p>

            <div
              className={
                stats.data.pendingCompaction > 0
                  ? "rounded-lg border border-accent bg-accent/10 px-4 py-3"
                  : "rounded-lg border border-border bg-surface px-4 py-3"
              }
            >
              <p className="text-sm font-semibold">
                {stats.data.pendingCompaction} record
                {stats.data.pendingCompaction === 1 ? "" : "s"} pending compaction
              </p>
              <p className="mt-0.5 text-xs text-muted">
                {stats.data.pendingCompaction > 0
                  ? "Run the compaction loop to merge near-duplicate and stale clusters."
                  : "The store is fully compacted."}
              </p>
            </div>

            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              <StatsTile label="Active" value={stats.data.active} />
              <StatsTile label="Superseded" value={stats.data.superseded} />
              <StatsTile label="Tombstoned" value={stats.data.tombstoned} />
              <StatsTile
                label="Idempotency conflicts"
                value={stats.data.idempotencyConflicts}
              />
            </div>

            <div className="rounded-lg border border-border bg-surface p-4">
              <h2 className="text-sm font-semibold">Active by scope</h2>
              <dl className="mt-2 divide-y divide-border">
                {scopes.map(([scope, count]) => (
                  <div key={scope} className="flex items-center justify-between py-2 text-sm">
                    <dt className="text-muted">{scope}</dt>
                    <dd className="font-medium">{count.toLocaleString()}</dd>
                  </div>
                ))}
              </dl>
            </div>

            <div className="rounded-lg border border-border bg-surface p-4">
              <h2 className="text-sm font-semibold">By tome</h2>
              {tomeRows.length === 0 ? (
                <p className="mt-2 text-xs text-muted">No tome breakdown reported yet.</p>
              ) : (
                <dl className="mt-2 divide-y divide-border">
                  {tomeRows.map(([tome, counts]) => (
                    <div key={tome} className="flex items-center justify-between py-2 text-sm">
                      <dt className="text-muted">{tome}</dt>
                      <dd className="font-medium">
                        {counts.active.toLocaleString()} active ·{" "}
                        {counts.tombstoned.toLocaleString()} tombstoned
                      </dd>
                    </div>
                  ))}
                </dl>
              )}
            </div>
          </div>
        )
      )}

      <div className="mt-4">
        {health.isPending ? (
          <p className="rounded-lg border border-dashed border-border px-6 py-8 text-center text-sm text-muted">
            Checking provider health…
          </p>
        ) : health.error ? (
          <QueryError message={health.error.message} onRetry={() => void health.refetch()} />
        ) : (
          health.data && <ProviderHealthCard health={health.data} />
        )}
      </div>
    </>
  );
}
