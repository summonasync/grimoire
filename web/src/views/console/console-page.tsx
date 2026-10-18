import type { ReactNode } from "react";
import type { TabId } from "@/shared/navigation/tabs";
import { AppShell } from "@/widgets/app-shell/app-shell";
import { MemoriesPage } from "@/views/memories/memories-page";
import { PersonaPage } from "@/views/persona/persona-page";
import { SettingsPage } from "@/views/settings/settings-page";
import { StatsPage } from "@/views/stats/stats-page";

function buildTabs(): Record<TabId, ReactNode> {
  return {
    memories: <MemoriesPage />,
    persona: <PersonaPage />,
    settings: <SettingsPage />,
    stats: <StatsPage />,
  };
}

export function ConsolePage() {
  return <AppShell tabs={buildTabs()} />;
}
