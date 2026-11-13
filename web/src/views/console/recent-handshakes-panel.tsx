"use client";

import { useQuery } from "@tanstack/react-query";
import { sessionsQueryOptions } from "@/shared/api/sessions";
import type { SessionHandshakeItem } from "@/shared/api/types";
import { absoluteTime, relativeTime } from "@/shared/ui/format";
import { Panel } from "@/shared/ui/panel";
import { EmptyState, NotTracked, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { Sigil } from "@/shared/ui/sigil";

function orUnknown(value: string | null): string {
  return value && value.trim().length > 0 ? value : "unknown";
}

function actorSeed(row: SessionHandshakeItem): string {
  return row.actorAlias ?? row.clientName ?? "unknown";
}

function actorText(row: SessionHandshakeItem): string {
  const alias = orUnknown(row.actorAlias);
  return row.actorVendor ? `${alias}+${row.actorVendor}` : alias;
}

function handshakeRow(row: SessionHandshakeItem, index: number) {
  return (
    <li key={`${row.occurredAt}-${index}`} className="flex items-start gap-2.5">
      <Sigil seed={actorSeed(row)} className="mt-1 size-4 shrink-0" />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm">
          {orUnknown(row.clientName)}
          <span className="ml-1.5 text-xs tabular-nums text-muted">
            {orUnknown(row.clientVersion)}
          </span>
        </p>
        <p className="mt-0.5 truncate text-[0.7rem] text-muted">
          protocol {orUnknown(row.protocolVersion)} · actor {actorText(row)}
        </p>
      </div>
      <span
        title={absoluteTime(row.occurredAt)}
        className="w-16 shrink-0 text-right text-[0.7rem] text-muted"
      >
        {relativeTime(row.occurredAt)}
      </span>
    </li>
  );
}

export function RecentHandshakesPanel() {
  const sessions = useQuery(sessionsQueryOptions());
  const data = sessions.data;
  const degraded = data?.degradationReason ?? null;

  return (
    <Panel
      title="Recent MCP handshakes"
      description={
        data
          ? `Clients the server saw at initialize · last ${data.windowHours}h`
          : "Clients the server saw at initialize"
      }
      icon="link"
    >
      {sessions.isPending ? (
        <div className="space-y-2">
          {[0, 1, 2].map((index) => (
            <Skeleton key={index} className="h-11" />
          ))}
        </div>
      ) : sessions.error ? (
        <QueryFailure
          message={sessions.error.message}
          onRetry={() => void sessions.refetch()}
        />
      ) : data ? (
        <div className="space-y-3">
          {degraded && (
            <NotTracked label="Handshake registry unavailable" reason={degraded} />
          )}
          {data.items.length === 0 ? (
            degraded ? null : (
              <EmptyState
                icon="spark"
                title="No handshakes in the window"
                reason={`The registry has no recorded MCP handshake newer than the last ${data.windowHours} hours. A client appears here after its first request.`}
              />
            )
          ) : (
            <ul className="space-y-2.5">{data.items.map(handshakeRow)}</ul>
          )}
        </div>
      ) : null}
    </Panel>
  );
}
