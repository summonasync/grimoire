"use client";

import Image from "next/image";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/shared/api/health";
import { memoriesQueryOptions } from "@/features/memories";
import { statsQueryOptions } from "@/features/stats";
import { compactNumber, relativeTime } from "@/shared/ui/format";
import { Donut, Meter, resolveSliceColor } from "@/shared/ui/charts";
import { Panel } from "@/shared/ui/panel";
import { StatTile } from "@/shared/ui/stat-tile";
import { EmptyState, NotTracked, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { KindBadge, ScopeBadge, StatusBadge } from "@/shared/ui/badge";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";

const scopeOrder = ["session", "project", "global", "personal"] as const;

function QuickAction({
  href,
  icon,
  title,
  subtitle,
}: {
  href: string;
  icon: "book" | "scry" | "plus";
  title: string;
  subtitle: string;
}) {
  return (
    <Link
      href={href}
      className="flex items-center gap-2.5 rounded-lg border border-border-strong/80 bg-background/50 px-3.5 py-2.5 transition-colors hover:border-accent hover:bg-accent-soft"
    >
      <Icon name={icon} className="size-4 shrink-0 text-accent" />
      <span className="min-w-0 text-left">
        <span className="block text-sm leading-tight font-medium">{title}</span>
        <span className="block text-[0.7rem] text-muted">{subtitle}</span>
      </span>
    </Link>
  );
}

export function ConsoleScreen() {
  const stats = useQuery(statsQueryOptions());
  const health = useQuery(healthQueryOptions());
  const recent = useQuery(memoriesQueryOptions({ limit: 8, status: "all" }));

  if (stats.error) {
    return (
      <QueryFailure message={stats.error.message} onRetry={() => void stats.refetch()} />
    );
  }

  if (stats.isPending || !stats.data) {
    return (
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {[0, 1, 2, 3].map((index) => (
          <Skeleton key={index} className="h-20" />
        ))}
      </div>
    );
  }

  const data = stats.data;
  const tomes = Object.entries(data.byTome);
  const total = data.active + data.superseded + data.tombstoned;
  const scopeSlices = scopeOrder
    .map((scope, index) => ({
      label: scope,
      value: data.activeByScope[scope] ?? 0,
      color: resolveSliceColor(index),
    }))
    .filter((slice) => slice.value > 0);
  const maxTome = tomes.reduce((max, [, counts]) => Math.max(max, counts.active), 0);
  const healthy = health.data?.status === "healthy";

  return (
    <div className="space-y-5">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="font-display text-3xl leading-tight font-semibold tracking-tight">
            Console
          </h1>
          <p className="mt-1 text-sm text-muted">
            Your agents remember. Here is what is happening.
          </p>
        </div>
        <div className="flex items-center gap-2 text-xs">
          <span
            className={
              healthy
                ? "rounded-md border border-jade/45 bg-jade/10 px-2 py-1 font-medium text-jade"
                : "rounded-md border border-ember/45 bg-ember/10 px-2 py-1 font-medium text-ember"
            }
          >
            {health.isPending ? "probing…" : (health.data?.status ?? "unknown")}
          </span>
          <span className="text-muted">resolved tome: {data.tome}</span>
        </div>
      </header>

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <StatTile
          icon="book"
          tone="accent"
          label="Tomes"
          value={tomes.length}
          hint="Knowledge vaults"
        />
        <StatTile
          icon="database"
          tone="jade"
          label="Total memories"
          value={compactNumber(total)}
          hint={`${compactNumber(data.active)} active`}
        />
        <StatTile
          icon="pulse"
          tone="gilt"
          label="Pending compaction"
          value={compactNumber(data.pendingCompaction)}
          hint={data.pendingCompaction > 0 ? "Candidates ready" : "Store is compact"}
        />
        <StatTile
          icon="alert"
          tone={data.idempotencyConflicts > 0 ? "ember" : "neutral"}
          label="Idempotency conflicts"
          value={compactNumber(data.idempotencyConflicts)}
          hint={data.idempotencyConflicts > 0 ? "Duplicate merges seen" : "None recorded"}
        />
      </div>

      <section className="panel relative overflow-hidden">
        <Image
          src="/design/hero-console.webp"
          alt=""
          aria-hidden
          fill
          sizes="52% of viewport"
          className="pointer-events-none absolute inset-y-0 right-0 hidden h-full w-[52%] object-cover object-left opacity-45 lg:block"
          style={{ maskImage: "var(--art-mask)", WebkitMaskImage: "var(--art-mask)" }}
        />
        <div className="relative z-10 max-w-xl px-5 py-6">
          <p className="font-display text-2xl leading-snug font-semibold italic text-gilt">
            “A memory well kept is an ally that never betrays.”
          </p>
          <p className="mt-2 text-sm text-muted">
            Your agents are learning, connecting and remembering across tomes.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            <QuickAction href="#memories" icon="book" title="Recall" subtitle="Search your memories" />
            <QuickAction href="#scry" icon="scry" title="Scry" subtitle="Explore other tomes" />
            <QuickAction href="#memories" icon="plus" title="Add memory" subtitle="Capture a new record" />
          </div>
        </div>
      </section>

      <div className="grid gap-4 xl:grid-cols-3">
        <Panel
          title="Memory distribution"
          description="Active entries by scope"
          icon="stats"
          className="xl:col-span-1"
        >
          {scopeSlices.length === 0 ? (
            <EmptyState
              icon="filter"
              title="Nothing active yet"
              reason="The engine reports no active records in any scope — write a memory and this fills in."
            />
          ) : (
            <Donut
              slices={scopeSlices}
              centerValue={compactNumber(data.active)}
              centerLabel="active"
            />
          )}
        </Panel>

        <Panel
          title="Writes over time"
          description="Memory writes per hour"
          icon="pulse"
          className="xl:col-span-1"
        >
          <NotTracked
            label="No time series in the API"
            reason="Stats returns current counts only — the engine does not expose a per-hour write history yet."
          />
          <div className="mt-3 space-y-2 text-xs">
            <div className="flex justify-between">
              <span className="text-muted">Superseded</span>
              <span className="tabular-nums">{compactNumber(data.superseded)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-muted">Tombstoned</span>
              <span className="tabular-nums">{compactNumber(data.tombstoned)}</span>
            </div>
          </div>
        </Panel>

        <Panel title="System health" description="Engine probes" icon="pulse" className="xl:col-span-1">
          <ul className="space-y-2 text-sm">
            <li className="flex items-center justify-between gap-3">
              <span className="flex items-center gap-2">
                <span
                  aria-hidden
                  className={
                    health.data?.store.reachable
                      ? "size-2 rounded-full bg-jade"
                      : "size-2 rounded-full bg-ember"
                  }
                />
                Vector store
              </span>
              <span className="text-xs text-muted">
                {health.data?.store.reachable ? "reachable" : (health.data?.store.error ?? "unreachable")}
              </span>
            </li>
            <li className="flex items-center justify-between gap-3">
              <span className="flex items-center gap-2">
                <span
                  aria-hidden
                  className={
                    health.data?.embeddings.reachable
                      ? "size-2 rounded-full bg-jade"
                      : "size-2 rounded-full bg-ember"
                  }
                />
                Embeddings provider
              </span>
              <span className="text-xs text-muted">
                {health.data?.embeddings.reachable
                  ? "reachable"
                  : (health.data?.embeddings.error ?? "unreachable")}
              </span>
            </li>
          </ul>
          <NotTracked
            className="mt-3"
            label="Per-component latency"
            reason="Health probes report reachability only — the API does not measure response times."
          />
        </Panel>
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Panel
          title="Recent activity"
          description="Newest records in the resolved tome"
          icon="clock"
          className="xl:col-span-2"
          bodyClassName="p-0"
        >
          {recent.isPending ? (
            <div className="space-y-2 p-4">
              {[0, 1, 2].map((index) => (
                <Skeleton key={index} className="h-12" />
              ))}
            </div>
          ) : recent.error ? (
            <div className="p-4">
              <QueryFailure
                message={recent.error.message}
                onRetry={() => void recent.refetch()}
              />
            </div>
          ) : recent.data && recent.data.items.length === 0 ? (
            <div className="p-4">
              <EmptyState
                icon="book"
                title="The tome is empty"
                reason={`No records were found in '${recent.data.tome}'. Agents write here through memory_remember.`}
              />
            </div>
          ) : (
            <ul className="divide-y divide-border">
              {recent.data?.items.map((record) => (
                <li key={record.id} className="flex items-start gap-3 px-4 py-2.5">
                  <Sigil seed={record.actor.alias} className="mt-1 size-4 shrink-0" />
                  <p className="line-clamp-1 min-w-0 flex-1 text-sm">{record.content}</p>
                  <span className="flex shrink-0 items-center gap-1.5">
                    <KindBadge kind={record.kind} />
                    <ScopeBadge scope={record.scope} />
                    <StatusBadge status={record.status} />
                  </span>
                  <span className="w-16 shrink-0 text-right text-[0.7rem] text-muted">
                    {relativeTime(record.createdAt)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel title="Tome activity" description="Active entries per tome" icon="book">
          {tomes.length === 0 ? (
            <EmptyState
              icon="book"
              title="No tomes yet"
              reason="The engine has not reported a tome breakdown — it appears once records exist."
            />
          ) : (
            <ul className="space-y-3">
              {tomes.map(([tome, counts]) => (
                <li key={tome}>
                  <div className="flex items-center gap-2 text-sm">
                    <Sigil seed={tome} className="size-4 shrink-0" />
                    <span className="min-w-0 flex-1 truncate">{tome}</span>
                    <span className="shrink-0 text-xs tabular-nums text-muted">
                      {compactNumber(counts.active)}
                    </span>
                  </div>
                  <Meter
                    className="mt-1.5"
                    value={counts.active}
                    max={maxTome}
                    color="var(--accent)"
                  />
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </div>
  );
}
