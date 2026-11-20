"use client";

import Image from "next/image";
import { useEffect, useState, type ReactNode } from "react";
import { ShelfToastProvider } from "@/features/shelf";
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
  const [navOpen, setNavOpen] = useState(false);

  useEffect(() => {
    const close = () => setNavOpen(false);
    window.addEventListener("hashchange", close);
    return () => window.removeEventListener("hashchange", close);
  }, []);

  useEffect(() => {
    if (!navOpen) {
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setNavOpen(false);
    };
    window.addEventListener("keydown", onKey);
    document.querySelector<HTMLElement>("aside nav a")?.focus();
    return () => window.removeEventListener("keydown", onKey);
  }, [navOpen]);

  return (
    <TomeFilterProvider>
      <ShelfToastProvider>
        <div className="relative flex min-h-dvh w-full">
          <a
            href="#main"
            onClick={(event) => {
              event.preventDefault();
              document.getElementById("main")?.focus();
            }}
            className="sr-only focus:not-sr-only focus:absolute focus:left-3 focus:top-3 focus:z-50 focus:rounded-md focus:bg-surface-raised focus:px-3 focus:py-2 focus:text-sm focus:ring-2 focus:ring-jade/60"
          >
            Skip to content
          </a>
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
                // Even a hairline blur pulls transparent pixels inward at the edges.
                transform: "scale(1.01)",
              }}
            />
            <div className="absolute inset-0" style={{ backgroundColor: "var(--art-scrim)" }} />
          </div>

          {navOpen && (
            <div
              aria-hidden
              className="fixed inset-0 z-30 bg-black/50 md:hidden"
              onClick={() => setNavOpen(false)}
            />
          )}

          <aside
            className={cn(
              "glass-shell fixed inset-y-0 left-0 z-40 flex w-56 flex-col border-r border-border transition-transform duration-200 md:static md:visible md:z-20 md:shrink-0 md:translate-x-0",
              navOpen ? "translate-x-0" : "invisible -translate-x-full",
            )}
          >
            <div className="flex items-center gap-2.5 px-4 py-5">
              <span className="flex size-9 items-center justify-center rounded-lg border border-jade/40 bg-jade/10">
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
                          ? "bg-jade/10 font-medium text-foreground ring-1 ring-jade/45"
                          : "text-muted hover:bg-foreground/5 hover:text-foreground",
                      )}
                    >
                      <Icon
                        name={item.icon}
                        className={cn("size-4", active === item.id && "text-jade")}
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
              <button
                type="button"
                aria-label="Open navigation"
                aria-expanded={navOpen}
                onClick={() => setNavOpen(true)}
                className="mr-auto rounded-md border border-border p-2 text-muted transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-jade/60 md:hidden"
              >
                <Icon name="menu" className="size-4" />
              </button>
              <ThemeToggle />
            </header>
            <main id="main" tabIndex={-1} className="min-w-0 flex-1 px-6 py-6 focus:outline-none">
              {tabs[active]}
            </main>
          </div>
        </div>
      </ShelfToastProvider>
    </TomeFilterProvider>
  );
}
