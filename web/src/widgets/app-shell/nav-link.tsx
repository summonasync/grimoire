"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/shared/lib/cn";
import type { NavItem } from "./nav-items";

export function NavLink({ item }: { item: NavItem }) {
  const pathname = usePathname();
  const active = pathname === item.href || pathname?.startsWith(`${item.href}/`) === true;

  return (
    <Link
      href={item.href}
      aria-current={active ? "page" : undefined}
      className={cn(
        "block whitespace-nowrap rounded-md px-3 py-2 text-sm transition-colors",
        active
          ? "bg-accent text-accent-foreground"
          : "text-muted hover:bg-surface hover:text-foreground",
      )}
    >
      {item.label}
    </Link>
  );
}
