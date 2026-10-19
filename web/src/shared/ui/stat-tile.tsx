import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { Icon, type IconName } from "./icons";

export type TileTone = "neutral" | "jade" | "gilt" | "ember" | "accent";

const toneRing: Record<TileTone, string> = {
  neutral: "border-border-strong/70",
  jade: "border-jade/40",
  gilt: "border-gilt/40",
  ember: "border-ember/45",
  accent: "border-accent/45",
};

const toneIcon: Record<TileTone, string> = {
  neutral: "text-muted",
  jade: "text-jade",
  gilt: "text-gilt",
  ember: "text-ember",
  accent: "text-accent",
};

interface StatTileProps {
  icon: IconName;
  tone?: TileTone;
  label: string;
  value: ReactNode;
  hint?: ReactNode;
  footer?: ReactNode;
}

export function StatTile({ icon, tone = "neutral", label, value, hint, footer }: StatTileProps) {
  return (
    <div className={cn("panel flex items-start gap-3 px-4 py-3.5", toneRing[tone])}>
      <span
        className={cn(
          "flex size-9 shrink-0 items-center justify-center rounded-lg border border-border bg-background/50",
          toneIcon[tone],
        )}
      >
        <Icon name={icon} className="size-4" />
      </span>
      <div className="min-w-0">
        <p className="text-xs text-muted">{label}</p>
        <p className="mt-0.5 font-display text-2xl leading-none font-semibold tabular-nums">
          {value}
        </p>
        {hint && <p className="mt-1 truncate text-[0.7rem] text-muted">{hint}</p>}
      </div>
      {footer && <div className="ml-auto shrink-0 self-end text-right">{footer}</div>}
    </div>
  );
}
