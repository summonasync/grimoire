import { cn } from "@/shared/lib/cn";
import { Icon } from "./icons";

interface NotTrackedProps {
  label: string;
  reason?: string;
  className?: string;
}

export function NotTracked({ label, reason, className }: NotTrackedProps) {
  return (
    <div
      className={cn(
        "flex items-start gap-2 rounded-lg border border-dashed border-border-strong/70 bg-foreground/[0.02] px-3 py-2.5",
        className,
      )}
    >
      <Icon name="alert" className="mt-0.5 size-3.5 shrink-0 text-muted" />
      <div className="min-w-0">
        <p className="text-xs font-medium text-muted">{label}</p>
        <p className="mt-0.5 text-[0.7rem] leading-snug text-muted/80">
          {reason ?? "Not tracked by the engine."}
        </p>
      </div>
    </div>
  );
}

type EmptyStateAction = { label: string; href: string } | { label: string; onClick: () => void };

interface EmptyStateProps {
  icon?: "search" | "book" | "memories" | "scry" | "filter" | "spark";
  title: string;
  reason: string;
  action?: EmptyStateAction;
  className?: string;
}

const emptyStateActionClasses =
  "mt-4 rounded-lg border border-border-strong px-3 py-1.5 text-xs text-muted transition-colors hover:border-jade hover:text-jade focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-jade/60";

export function EmptyState({
  icon = "spark",
  title,
  reason,
  action,
  className,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center justify-center rounded-xl border border-dashed border-border-strong/70 px-6 py-12 text-center",
        className,
      )}
    >
      <span className="mb-3 flex size-11 items-center justify-center rounded-full border border-border-strong text-muted">
        <Icon name={icon} className="size-5" />
      </span>
      <p className="font-display text-base font-semibold">{title}</p>
      <p className="mt-1 max-w-sm text-sm text-muted">{reason}</p>
      {action &&
        ("href" in action ? (
          <a href={action.href} className={emptyStateActionClasses}>
            {action.label}
          </a>
        ) : (
          <button type="button" onClick={action.onClick} className={emptyStateActionClasses}>
            {action.label}
          </button>
        ))}
    </div>
  );
}

interface QueryFailureProps {
  message: string;
  onRetry: () => void;
  className?: string;
}

export function QueryFailure({ message, onRetry, className }: QueryFailureProps) {
  return (
    <div
      role="alert"
      className={cn(
        "rounded-xl border border-ember/45 bg-ember/8 px-5 py-6 text-center",
        className,
      )}
    >
      <p className="flex items-center justify-center gap-2 text-sm font-medium text-ember">
        <Icon name="alert" className="size-4" />
        The engine could not answer.
      </p>
      <p className="mt-1 text-xs break-words text-muted">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="mt-3 rounded-lg border border-border-strong px-3 py-1.5 text-xs text-muted transition-colors hover:border-jade hover:text-jade"
      >
        Retry
      </button>
    </div>
  );
}

export function Skeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn("animate-pulse rounded-lg border border-border bg-foreground/5", className)}
      aria-hidden
    />
  );
}
