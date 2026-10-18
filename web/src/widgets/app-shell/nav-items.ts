import type { TabId } from "@/shared/navigation/tabs";

export interface TabItem {
  id: TabId;
  label: string;
}

export const tabItems: TabItem[] = [
  { id: "memories", label: "Memories" },
  { id: "persona", label: "Persona" },
  { id: "settings", label: "Settings" },
  { id: "stats", label: "Stats" },
];
