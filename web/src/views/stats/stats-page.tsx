"use client";

import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/shared/api/health";
import { PageHeader } from "@/shared/ui/page-header";
import { ProviderHealthCard } from "@/features/stats/components/provider-health-card";
import { statsQueryOptions } from "@/features/stats/queries";
import { Donut, resolveSliceColor } from "@/shared/ui/charts";
import { Panel } from "@/shared/ui/panel";
import { StatTile } from "@/shared/ui/stat-tile";
import { EmptyState, NotTracked, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { absoluteTime, compactNumber, relativeTime } from "@/shared/ui/format";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";

export function StatsPage() {
  const stats = useQuery(statsQueryOptions());
  const health = useQuery(healthQueryOptions());

  if (stats.error) {
    return <QueryFailure message={stats.error.message} onRetry={() => void stats.refetch()} />;
  }

  if (stats.isPending || !stats.data) {
    return (
      <>
        <PageHeader title="Stats" description="Memory system health and telemetry." />
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2, 3, 4, 5].map((index) => (
            <Skeleton key={index} className="h-20" />
          ))}
        </div>
      </>
    );
  }

  const data = stats.data;
  const tomeRows = Object.entries(data.byTome);
  const tomeSlices = tomeRows.map(([tome, counts], index) => ({
    label: tome,
    value: counts.active,
    color: resolveSliceColor(index),
  }));
  const totalActive = tomeRows.reduce((sum, [, counts]) => sum + counts.active, 0);
  const gc = data.gc;

  return (
    <div className="space-y-5">
      <PageHeader
        title="Stats"
        description="Memory system health and telemetry."
        actions={
          <span className="flex items-center gap-1.5 rounded-lg border border-border-strong bg-surface/80 px-2.5 py-1.5 text-xs">
            <Icon name="book" className="size-3.5 text-muted" />
            <span className="text-muted">Resolved tome</span>
            <span className="font-medium">{data.tome}</span>
          </span>
        }
      />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <StatTile
          icon="memories"
          tone="jade"
          label="Active entries"
          value={compactNumber(data.active)}
          hint="Usable, non-tombstoned records"
        />
        <StatTile
          icon="clock"
          tone="gilt"
          label="Superseded"
          value={compactNumber(data.superseded)}
          hint="Replaced by a newer truth"
        />
        <StatTile
          icon="tombstone"
          tone="neutral"
          label="Tombstoned"
          value={compactNumber(data.tombstoned)}
          hint="Soft-deleted, awaiting GC"
        />
        <StatTile
          icon="alert"
          tone={data.idempotencyConflicts > 0 ? "ember" : "jade"}
          label="Idempotency conflicts"
          value={compactNumber(data.idempotencyConflicts)}
          hint={data.idempotencyConflicts > 0 ? "Duplicate merges recorded" : "Zero is healthy"}
        />
        <StatTile
          icon="database"
          tone={data.pendingCompaction > 0 ? "gilt" : "jade"}
          label="Pending compaction"
          value={compactNumber(data.pendingCompaction)}
          hint={
            data.pendingCompaction > 0
              ? "Journal clusters ready to merge"
              : "Store is fully compacted"
          }
        />
        <StatTile
          icon="pulse"
          tone={gc.stale ? "ember" : "jade"}
          label="GC heartbeat"
          value={gc.lastSuccessAt ? relativeTime(gc.lastSuccessAt) : "never"}
          hint={
            gc.unknownReason
              ?? (gc.stale
                ? "Stale — no successful sweep recently"
                : `Last run removed ${compactNumber((gc.removedExpired ?? 0) + (gc.removedTombstones ?? 0))} records`)
          }
        />
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Panel
          title="Entries by tome"
          description="Distribution of active memory entries."
          icon="stats"
          className="xl:col-span-2"
        >
          {tomeSlices.length === 0 ? (
            <EmptyState
              icon="book"
              title="No tome breakdown yet"
              reason="The engine reports per-tome counts only once records exist across tomes."
            />
          ) : (
            <Donut
              slices={tomeSlices}
              centerValue={compactNumber(totalActive)}
              centerLabel="active"
            />
          )}
        </Panel>

        <Panel title="Storage footprint" description="On-disk cost by segment" icon="database">
          <NotTracked
            label="Storage metrics are not exposed"
            reason="The stats endpoint returns record counts only — no byte sizes for embeddings, metadata, content or indexes."
          />
        </Panel>
      </div>

      <Panel
        title="Per-tome statistics"
        description="Record counts by tome."
        icon="book"
        bodyClassName="p-0"
      >
        {tomeRows.length === 0 ? (
          <div className="p-4">
            <EmptyState
              icon="book"
              title="Nothing to break down"
              reason="No tomes reported — write a memory and the engine will list it here."
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full border-collapse text-left text-sm">
              <thead>
                <tr className="border-b border-border-strong/70">
                  <th className="label px-4 py-2">Tome</th>
                  <th className="label px-4 py-2 text-right">Active</th>
                  <th className="label px-4 py-2 text-right">Tombstoned</th>
                  <th className="label px-4 py-2 text-right">Share</th>
                  <th className="label px-4 py-2 text-right">Last write</th>
                </tr>
              </thead>
              <tbody>
                {tomeRows.map(([tome, counts]) => (
                  <tr key={tome} className="border-b border-border/60 last:border-0">
                    <td className="px-4 py-2">
                      <span className="flex items-center gap-2">
                        <Sigil seed={tome} className="size-4 shrink-0" />
                        {tome}
                      </span>
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums">
                      {counts.active.toLocaleString()}
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums text-muted">
                      {counts.tombstoned.toLocaleString()}
                    </td>
                    <td className="px-4 py-2 text-right tabular-nums text-muted">
                      {totalActive > 0
                        ? `${Math.round((counts.active / totalActive) * 100)}%`
                        : "—"}
                    </td>
                    <td
                      className="px-4 py-2 text-right tabular-nums text-muted"
                      title={absoluteTime(counts.lastWriteAt)}
                    >
                      {relativeTime(counts.lastWriteAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>

      <div className="grid gap-4 xl:grid-cols-3">
        <Panel title="Provider health" description="Engine dependency probes" icon="pulse">
          {health.isPending ? (
            <Skeleton className="h-24" />
          ) : health.error ? (
            <QueryFailure message={health.error.message} onRetry={() => void health.refetch()} />
          ) : (
            health.data && <ProviderHealthCard health={health.data} />
          )}
        </Panel>

        <Panel title="Writes over time" description="Hourly write history" icon="stats">
          <NotTracked
            label="No time series in the API"
            reason="Stats reports current totals; the engine keeps no per-hour write, tombstone or conflict history to chart."
          />
        </Panel>

        <Panel title="Compaction queue" description="Candidates per tome" icon="database">
          <NotTracked
            label="Queue is not per-tome"
            reason={`Only a global pending count is exposed (${compactNumber(data.pendingCompaction)}). Reclaimable bytes, oldest candidate and a manual run trigger do not exist in the API.`}
          />
        </Panel>
      </div>
    </div>
  );
}
