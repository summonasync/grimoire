"use client";

import { useQuery } from "@tanstack/react-query";
import { agentsQueryOptions } from "@/shared/api/agents";
import { compactNumber, relativeTime } from "@/shared/ui/format";
import { Panel } from "@/shared/ui/panel";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { Sigil } from "@/shared/ui/sigil";

export function RecentAgentsPanel() {
  const agents = useQuery(agentsQueryOptions());

  return (
    <Panel
      title="Recent agents"
      description="Who touched the store in the last 30 days"
      icon="pulse"
    >
      {agents.isPending ? (
        <div className="space-y-2">
          {[0, 1, 2].map((index) => (
            <Skeleton key={index} className="h-9" />
          ))}
        </div>
      ) : agents.error ? (
        <QueryFailure message={agents.error.message} onRetry={() => void agents.refetch()} />
      ) : agents.data && agents.data.agents.length === 0 ? (
        <EmptyState
          icon="spark"
          title="No agent activity"
          reason="Nothing was written to the store in the last 30 days."
        />
      ) : (
        <ul className="space-y-2.5">
          {agents.data?.agents.map((actor) => (
            <li key={actor.alias} className="flex items-center gap-2.5 text-sm">
              <Sigil seed={actor.alias} className="size-4 shrink-0" />
              <span className="min-w-0 flex-1 truncate">
                {actor.vendor ? `${actor.alias}+${actor.vendor}` : actor.alias}
              </span>
              {actor.isHuman && (
                <span className="shrink-0 rounded border border-gilt/45 bg-gilt/10 px-1.5 py-0.5 text-[0.65rem] font-medium text-gilt">
                  human
                </span>
              )}
              <span className="w-16 shrink-0 text-right text-[0.7rem] text-muted">
                {relativeTime(actor.lastActivity)}
              </span>
              <span className="w-10 shrink-0 text-right text-xs tabular-nums text-muted">
                {compactNumber(actor.recordCount)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}
