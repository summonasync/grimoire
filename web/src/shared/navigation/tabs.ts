export const tabIds = ["console", "memories", "persona", "stats", "settings", "scry"] as const;

export type TabId = (typeof tabIds)[number];
