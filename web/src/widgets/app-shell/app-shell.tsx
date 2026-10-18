import type { ReactNode } from "react";
import { NavLink } from "./nav-link";
import { navItems } from "./nav-items";
import { ThemeToggle } from "./theme-toggle";

export function AppShell({ children }: { children: ReactNode }) {
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
          aria-label="Primary"
          className="border-b border-border px-3 py-2 sm:w-44 sm:shrink-0 sm:border-b-0 sm:border-r sm:py-4"
        >
          <ul className="flex gap-1 sm:flex-col">
            {navItems.map((item) => (
              <li key={item.href}>
                <NavLink item={item} />
              </li>
            ))}
          </ul>
        </nav>
        <main className="min-w-0 flex-1 px-5 py-6">{children}</main>
      </div>
    </div>
  );
}
