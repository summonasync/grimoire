import type { IconName } from "@/shared/ui/icons";
import type { TabId } from "@/shared/navigation/tabs";

export interface TabItem {
  id: TabId;
  label: string;
  icon: IconName;
  art: string;
}

export const tabItems: TabItem[] = [
  { id: "console", label: "Console", icon: "console", art: "/design/hero-console.webp" },
  { id: "shelf", label: "Shelf", icon: "shelf", art: "/design/art-memories.webp" },
  { id: "memories", label: "Memories", icon: "memories", art: "/design/art-memories.webp" },
  { id: "persona", label: "Persona", icon: "persona", art: "/design/art-persona.webp" },
  { id: "stats", label: "Stats", icon: "stats", art: "/design/art-stats.webp" },
  { id: "settings", label: "Settings", icon: "settings", art: "/design/art-settings.webp" },
  { id: "scry", label: "Scry", icon: "scry", art: "/design/art-scry.webp" },
];
