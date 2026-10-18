import type { MemoryStatus } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";

const statusStyles: Record<MemoryStatus, string> = {
  active: "border-accent/50 text-accent",
  superseded: "border-border text-muted",
  tombstoned: "border-border text-muted line-through decoration-1",
};

interface MemoryStatusBadgeProps {
  status: MemoryStatus;
}

export function MemoryStatusBadge({ status }: MemoryStatusBadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-xs font-medium",
        statusStyles[status],
      )}
    >
      {status}
    </span>
  );
}
