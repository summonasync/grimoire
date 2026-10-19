import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { Icon, type IconName } from "./icons";

interface PanelProps {
  title?: string;
  description?: string;
  icon?: IconName;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  bodyClassName?: string;
  as?: "section" | "div" | "aside";
}

export function Panel({
  title,
  description,
  icon,
  actions,
  children,
  className,
  bodyClassName,
  as: Tag = "section",
}: PanelProps) {
  return (
    <Tag className={cn("panel flex min-w-0 flex-col", className)}>
      {(title !== undefined || actions !== undefined) && (
        <header className="flex items-start justify-between gap-3 border-b border-border px-4 py-3">
          <div className="min-w-0">
            <h2 className="flex items-center gap-2 font-display text-base leading-tight font-semibold">
              {icon && <Icon name={icon} className="size-4 text-accent" />}
              {title}
            </h2>
            {description && <p className="mt-0.5 text-xs text-muted">{description}</p>}
          </div>
          {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
        </header>
      )}
      <div className={cn("min-w-0 flex-1 p-4", bodyClassName)}>{children}</div>
    </Tag>
  );
}
