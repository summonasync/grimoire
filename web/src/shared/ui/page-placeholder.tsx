import type { ReactNode } from "react";

interface PagePlaceholderProps {
  children: ReactNode;
}

export function PagePlaceholder({ children }: PagePlaceholderProps) {
  return (
    <div className="rounded-lg border border-dashed border-border bg-surface/70 px-6 py-10 text-center text-sm text-muted">
      {children}
    </div>
  );
}
