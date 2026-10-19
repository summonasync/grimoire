import type { HealthResponse } from "@/shared/api/health";
import { cn } from "@/shared/lib/cn";
import { absoluteTime, relativeTime } from "@/shared/ui/format";
import { Icon } from "@/shared/ui/icons";

interface ProviderHealthCardProps {
  health: HealthResponse;
}

function HealthRow({
  label,
  reachable,
  error,
}: {
  label: string;
  reachable: boolean;
  error: string | null;
}) {
  return (
    <div className="flex items-start justify-between gap-3 py-2">
      <span className="flex items-center gap-2 text-sm">
        <span
          aria-hidden
          className={cn("size-2 rounded-full", reachable ? "bg-jade" : "bg-ember")}
        />
        {label}
      </span>
      <span className="max-w-56 truncate text-right text-xs text-muted" title={error ?? undefined}>
        {reachable ? "reachable" : (error ?? "unreachable")}
      </span>
    </div>
  );
}

export function ProviderHealthCard({ health }: ProviderHealthCardProps) {
  return (
    <div>
      <div className="mb-2 flex items-center justify-between gap-2">
        <span
          className={cn(
            "flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-xs font-medium",
            health.status === "healthy"
              ? "border-jade/45 bg-jade/10 text-jade"
              : "border-ember/45 bg-ember/10 text-ember",
          )}
        >
          <Icon name={health.status === "healthy" ? "check" : "alert"} className="size-3.5" />
          {health.status}
        </span>
        <span className="text-[0.7rem] text-muted" title={absoluteTime(health.checkedAt)}>
          checked {relativeTime(health.checkedAt)}
        </span>
      </div>
      <div className="divide-y divide-border">
        <HealthRow label="Vector store" reachable={health.store.reachable} error={health.store.error} />
        <HealthRow
          label="Embeddings provider"
          reachable={health.embeddings.reachable}
          error={health.embeddings.error}
        />
      </div>
    </div>
  );
}
