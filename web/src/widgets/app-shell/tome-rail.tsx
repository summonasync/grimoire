"use client";

import { useQuery } from "@tanstack/react-query";
import { statsQueryOptions } from "@/features/stats";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";
import { useTomeFilter } from "./tome-context";

export function TomeRail() {
  const stats = useQuery(statsQueryOptions());
  const { tome: active, setTome } = useTomeFilter();
  const rows = stats.data ? Object.entries(stats.data.byTome) : [];

  return (
    <nav aria-label="Tomes" className="mt-6 min-h-0 flex-1 overflow-y-auto px-3">
      <p className="label px-1 pb-2">Tomes</p>
      {rows.length === 0 ? (
        <p className="px-1 text-xs text-muted">
          {stats.isPending ? "Reading the vault…" : "No tomes reported yet."}
        </p>
      ) : (
        <ul className="space-y-0.5">
          {rows.map(([tome, counts]) => (
            <li key={tome}>
              <button
                type="button"
                data-unsaved-guard
                onClick={() => setTome(active === tome ? null : tome)}
                aria-current={active === tome ? "true" : undefined}
                className={cn(
                  "flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors",
                  active === tome
                    ? "bg-jade/10 text-foreground ring-1 ring-jade/45"
                    : "text-muted hover:bg-foreground/5 hover:text-foreground",
                )}
              >
                <Sigil seed={tome} className="size-4 shrink-0" />
                <span className="min-w-0 flex-1 truncate">{tome}</span>
                <span className="shrink-0 text-[0.68rem] tabular-nums text-muted">
                  {counts.active}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {active && (
        <button
          type="button"
          data-unsaved-guard
          onClick={() => setTome(null)}
          className="mt-2 flex items-center gap-1 px-2 text-[0.7rem] text-muted transition-colors hover:text-jade"
        >
          <Icon name="close" className="size-3" />
          Clear tome filter
        </button>
      )}
    </nav>
  );
}
