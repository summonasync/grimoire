export const tabIds = ["memories", "persona", "scry", "settings", "stats"] as const;

export type TabId = (typeof tabIds)[number];
