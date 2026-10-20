"use client";

import Image from "next/image";
import { type ReactNode } from "react";
import { cn } from "@/shared/lib/cn";
import { useHashTab } from "@/shared/lib/use-hash-tab";
import type { TabId } from "@/shared/navigation/tabs";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";
import { tabItems } from "./nav-items";
import { ThemeToggle } from "./theme-toggle";
import { TomeRail } from "./tome-rail";
import { EndpointPill } from "./endpoint-pill";
import { TomeFilterProvider } from "./tome-context";

interface AppShellProps {
  tabs: Record<TabId, ReactNode>;
}

export function AppShell({ tabs }: AppShellProps) {
  const active = useHashTab("console");
  const current = tabItems.find((item) => item.id === active) ?? tabItems[0]!;

  return (
    <TomeFilterProvider>
      <div className="relative flex min-h-dvh w-full">
        <div aria-hidden className="pointer-events-none fixed inset-0 z-0">
          <Image
            src={current.art}
            alt=""
            fill
            priority
            sizes="100vw"
            className="object-cover"
            style={{
              opacity: "var(--art-opacity)",
              filter: "blur(var(--art-blur))",
              // Blur pulls transparent pixels inward at the edges; oversizing hides the fringe.
              transform: "scale(1.08)",
            }}
          />
          <div className="absolute inset-0" style={{ backgroundColor: "var(--art-scrim)" }} />
        </div>

        <aside className="glass-shell relative z-20 flex w-56 shrink-0 flex-col border-r border-border">
          <div className="flex items-center gap-2.5 px-4 py-5">
            <span className="flex size-9 items-center justify-center rounded-lg border border-accent/40 bg-accent-soft">
              <Sigil seed="grimoire-wordmark" className="size-5" />
            </span>
            <div className="min-w-0">
              <p className="font-display text-lg leading-none font-semibold tracking-tight">
                Grimoire
              </p>
              <p className="mt-1 text-[0.65rem] tracking-wide text-muted uppercase">
                Local memory console
              </p>
            </div>
          </div>

          <nav aria-label="Console sections" className="px-3">
            <ul className="space-y-0.5">
              {tabItems.map((item) => (
                <li key={item.id}>
                  <a
                    href={`#${item.id}`}
                    aria-current={active === item.id ? "page" : undefined}
                    className={cn(
                      "flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm transition-colors",
                      active === item.id
                        ? "bg-accent-soft font-medium text-foreground ring-1 ring-accent/45"
                        : "text-muted hover:bg-foreground/5 hover:text-foreground",
                    )}
                  >
                    <Icon
                      name={item.icon}
                      className={cn("size-4", active === item.id && "text-accent")}
                    />
                    {item.label}
                  </a>
                </li>
              ))}
            </ul>
          </nav>

          <TomeRail />

          <div className="border-t border-border p-3">
            <EndpointPill />
          </div>
        </aside>

        <div className="relative z-10 flex min-w-0 flex-1 flex-col">
          <header className="flex items-center justify-end gap-2 border-b border-border/70 px-6 py-3">
            <ThemeToggle />
          </header>
          <main className="min-w-0 flex-1 px-6 py-6">{tabs[active]}</main>
        </div>
      </div>
    </TomeFilterProvider>
  );
}
