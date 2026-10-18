"use client";

import type { ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { useHashTab } from "@/shared/lib/use-hash-tab";
import type { TabId } from "@/shared/navigation/tabs";
import { tabItems } from "./nav-items";
import { ThemeToggle } from "./theme-toggle";

interface AppShellProps {
  tabs: Record<TabId, ReactNode>;
}

export function AppShell({ tabs }: AppShellProps) {
  const active = useHashTab("memories");

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-6xl flex-col">
      <header className="flex items-center justify-between border-b border-border px-5 py-4">
        <div className="flex items-baseline gap-2">
          <span className="text-base font-semibold tracking-tight">Grimoire</span>
          <span className="text-xs text-muted">agent memory console</span>
        </div>
        <ThemeToggle />
      </header>
      <div className="flex flex-1 flex-col sm:flex-row">
        <nav
          aria-label="Console sections"
          className="border-b border-border px-3 py-2 sm:w-44 sm:shrink-0 sm:border-b-0 sm:border-r sm:py-4"
        >
          <ul className="flex gap-1 sm:flex-col">
            {tabItems.map((item) => (
              <li key={item.id}>
                <a
                  href={`#${item.id}`}
                  aria-current={active === item.id ? "page" : undefined}
                  className={cn(
                    "block whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors",
                    active === item.id
                      ? "bg-accent text-accent-foreground"
                      : "text-muted hover:bg-surface hover:text-foreground",
                  )}
                >
                  {item.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>
        <main className="min-w-0 flex-1 px-5 py-6">{tabs[active]}</main>
      </div>
    </div>
  );
}
