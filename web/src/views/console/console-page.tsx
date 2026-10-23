import type { ReactNode } from "react";
import type { TabId } from "@/shared/navigation/tabs";
import { AppShell } from "@/widgets/app-shell/app-shell";
import { ConsoleScreen } from "./console-screen";
import { MemoriesPage } from "@/views/memories/memories-page";
import { PersonaPage } from "@/views/persona/persona-page";
import { ScryPage } from "@/views/scry/scry-page";
import { SettingsPage } from "@/views/settings/settings-page";
import { ShelfPage } from "@/views/shelf/shelf-page";
import { StatsPage } from "@/views/stats/stats-page";

function buildTabs(): Record<TabId, ReactNode> {
  return {
    console: <ConsoleScreen />,
    shelf: <ShelfPage />,
    memories: <MemoriesPage />,
    persona: <PersonaPage />,
    stats: <StatsPage />,
    settings: <SettingsPage />,
    scry: <ScryPage />,
  };
}

export function ConsolePage() {
  return <AppShell tabs={buildTabs()} />;
}
