export const tabIds = ["memories", "persona", "settings", "stats"] as const;

export type TabId = (typeof tabIds)[number];
