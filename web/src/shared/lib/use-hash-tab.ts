"use client";

import { useSyncExternalStore } from "react";
import { tabIds, type TabId } from "@/shared/navigation/tabs";

function subscribe(onChange: () => void): () => void {
  window.addEventListener("hashchange", onChange);
  return () => window.removeEventListener("hashchange", onChange);
}

function getSnapshot(): string {
  return window.location.hash;
}

function getServerSnapshot(): string {
  return "";
}

function parseTab(hash: string): TabId | null {
  const id = hash.replace(/^#/, "");
  return tabIds.find((tab) => tab === id) ?? null;
}

export function useHashTab(defaultTab: TabId): TabId {
  const hash = useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
  return parseTab(hash) ?? defaultTab;
}
