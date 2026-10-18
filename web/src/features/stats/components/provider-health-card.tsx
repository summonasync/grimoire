import type { HealthResponse } from "@/shared/api/health";
import { cn } from "@/shared/lib/cn";

interface ProviderHealthCardProps {
  health: HealthResponse;
}

function HealthDot({ reachable }: { reachable: boolean }) {
  return (
    <span
      aria-hidden
      className={cn("inline-block size-2 rounded-full", reachable ? "bg-accent" : "bg-red-500")}
    />
  );
}

function HealthRow({ label, reachable, error }: { label: string; reachable: boolean; error: string | null }) {
  return (
    <div className="flex items-start justify-between gap-3 py-2">
      <span className="flex items-center gap-2 text-sm">
        <HealthDot reachable={reachable} />
        {label}
      </span>
      <span className="max-w-64 text-right text-xs break-words text-muted">
        {reachable ? "reachable" : (error ?? "unreachable")}
      </span>
    </div>
  );
}

export function ProviderHealthCard({ health }: ProviderHealthCardProps) {
  return (
    <div className="rounded-lg border border-border bg-surface p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">Provider health</h2>
        <span
          className={cn(
            "rounded-full border px-2 py-0.5 text-xs font-medium",
            health.status === "healthy" ? "border-accent/50 text-accent" : "border-red-500/50 text-red-400",
          )}
        >
          {health.status}
        </span>
      </div>
      <div className="mt-2 divide-y divide-border">
        <HealthRow label="Store" reachable={health.store.reachable} error={health.store.error} />
        <HealthRow
          label="Embeddings provider"
          reachable={health.embeddings.reachable}
          error={health.embeddings.error}
        />
      </div>
      <p className="mt-2 text-xs text-muted">Checked {new Date(health.checkedAt).toLocaleString()}</p>
    </div>
  );
}
