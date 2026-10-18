export interface NavItem {
  href: string;
  label: string;
}

export const navItems: NavItem[] = [
  { href: "/memories", label: "Memories" },
  { href: "/persona", label: "Persona" },
  { href: "/settings", label: "Settings" },
  { href: "/stats", label: "Stats" },
];
