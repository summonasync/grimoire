"use client";

import { useQuery } from "@tanstack/react-query";
import { healthQueryOptions } from "@/shared/api/health";
import { settingsQueryOptions } from "@/features/settings";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";

export function EndpointPill() {
  const health = useQuery(healthQueryOptions());
  const settings = useQuery(settingsQueryOptions());
  const port = settings.data?.serve.port;
  const reachable = health.data?.status === "healthy";

  return (
    <div className="flex items-center gap-2 rounded-lg border border-border-strong/70 bg-background/60 px-2.5 py-2">
      <span
        aria-hidden
        className={cn(
          "size-2 shrink-0 rounded-full",
          reachable ? "bg-jade" : "bg-ember",
        )}
      />
      <span className="text-xs font-medium">Local</span>
      <span className="ml-auto font-mono text-[0.68rem] text-muted">
        {health.isPending ? "probing…" : `127.0.0.1${port ? `:${port}` : ""}`}
      </span>
      <Icon name="chevronDown" className="size-3 shrink-0 text-muted" />
    </div>
  );
}
