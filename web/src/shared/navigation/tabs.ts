export const tabIds = ["console", "shelf", "memories", "graph", "persona", "stats", "settings", "scry"] as const;

export type TabId = (typeof tabIds)[number];
