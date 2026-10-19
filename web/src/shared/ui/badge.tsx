import type { ReactNode } from "react";
import type {
  CorrectionAction,
  MemoryKind,
  MemoryScope,
  MemoryStatus,
} from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";

export type BadgeTone = "neutral" | "jade" | "gilt" | "ember" | "accent" | "muted";

const tones: Record<BadgeTone, string> = {
  neutral: "border-border-strong/70 text-foreground/85 bg-foreground/5",
  jade: "border-jade/45 text-jade bg-jade/10",
  gilt: "border-gilt/45 text-gilt bg-gilt/10",
  ember: "border-ember/45 text-ember bg-ember/10",
  accent: "border-accent/50 text-accent bg-accent-soft",
  muted: "border-border text-muted bg-transparent",
};

interface BadgeProps {
  tone?: BadgeTone;
  children: ReactNode;
  className?: string;
  title?: string;
}

export function Badge({ tone = "neutral", children, className, title }: BadgeProps) {
  return (
    <span
      title={title}
      className={cn(
        "inline-flex shrink-0 items-center gap-1 rounded-md border px-1.5 py-0.5 text-[0.68rem] font-medium tracking-wide",
        tones[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}

const scopeTones: Record<MemoryScope, BadgeTone> = {
  session: "muted",
  project: "accent",
  global: "gilt",
  personal: "jade",
};

export function ScopeBadge({ scope }: { scope: MemoryScope }) {
  return <Badge tone={scopeTones[scope]}>{scope}</Badge>;
}

const kindTones: Record<MemoryKind, BadgeTone> = {
  journal: "ember",
  fact: "jade",
};

export function KindBadge({ kind }: { kind: MemoryKind }) {
  return <Badge tone={kindTones[kind]}>{kind}</Badge>;
}

const statusTones: Record<MemoryStatus, BadgeTone> = {
  active: "jade",
  superseded: "gilt",
  tombstoned: "muted",
};

export function StatusBadge({
  status,
  className,
}: {
  status: MemoryStatus;
  className?: string;
}) {
  return (
    <Badge
      tone={statusTones[status]}
      className={cn(status === "tombstoned" && "line-through", className)}
    >
      {status}
    </Badge>
  );
}

const actionTones: Record<CorrectionAction, BadgeTone> = {
  patch: "accent",
  restore: "jade",
  supersede: "gilt",
  forget: "ember",
};

export function ActionBadge({ action }: { action: CorrectionAction }) {
  return <Badge tone={actionTones[action]}>{action}</Badge>;
}

export function TomeBadge({ tome }: { tome: string }) {
  return (
    <Badge tone="neutral" className="font-mono">
      {tome}
    </Badge>
  );
}
