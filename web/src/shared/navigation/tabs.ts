export const tabIds = ["console", "shelf", "memories", "persona", "stats", "settings", "scry"] as const;

export type TabId = (typeof tabIds)[number];
