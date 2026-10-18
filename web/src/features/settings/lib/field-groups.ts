import type { SettingsFieldKey } from "../types";

export interface SettingsFieldSpec {
  key: SettingsFieldKey;
  label: string;
  hint: string;
  min: number;
  max: number;
  step: number;
  integer: boolean;
}

export interface SettingsFieldGroup {
  id: string;
  title: string;
  description: string;
  fields: SettingsFieldSpec[];
}

export const settingsFieldGroups: SettingsFieldGroup[] = [
  {
    id: "memory",
    title: "Memory engine",
    description: "Remember and recall tuning. These are the keys PUT /api/settings would accept.",
    fields: [
      {
        key: "sessionTtlHours",
        label: "Session TTL (hours)",
        hint: "Session-scope memories expire after this many hours.",
        min: 0,
        max: 8760,
        step: 1,
        integer: false,
      },
      {
        key: "dedupSimilarityThreshold",
        label: "Dedup similarity threshold",
        hint: "Cosine similarity at which a new memory supersedes an existing one.",
        min: 0,
        max: 1,
        step: 0.01,
        integer: false,
      },
      {
        key: "tombstoneRetentionDays",
        label: "Tombstone retention (days)",
        hint: "Tombstones are hard-deleted by GC after this many days.",
        min: 0,
        max: 3650,
        step: 1,
        integer: false,
      },
      {
        key: "recallOversampling",
        label: "Recall oversampling",
        hint: "Extra candidates fetched before rescoring.",
        min: 1,
        max: 1000,
        step: 1,
        integer: true,
      },
    ],
  },
  {
    id: "half-lives",
    title: "Recency half-lives",
    description: "Score decay per scope, in days.",
    fields: [
      {
        key: "halfLives.sessionDays",
        label: "Session",
        hint: "Half-life for session-scope memories.",
        min: 0,
        max: 3650,
        step: 0.5,
        integer: false,
      },
      {
        key: "halfLives.projectDays",
        label: "Project",
        hint: "Half-life for project-scope memories.",
        min: 0,
        max: 3650,
        step: 0.5,
        integer: false,
      },
      {
        key: "halfLives.globalDays",
        label: "Global",
        hint: "Half-life for global-scope memories.",
        min: 0,
        max: 3650,
        step: 0.5,
        integer: false,
      },
      {
        key: "halfLives.personalDays",
        label: "Personal",
        hint: "Half-life for personal-scope memories.",
        min: 0,
        max: 3650,
        step: 0.5,
        integer: false,
      },
    ],
  },
  {
    id: "compaction",
    title: "Compaction",
    description: "Journal-to-knowledge clustering thresholds.",
    fields: [
      {
        key: "compaction.minSimilarity",
        label: "Min similarity",
        hint: "Cosine similarity required to join a cluster.",
        min: 0,
        max: 1,
        step: 0.01,
        integer: false,
      },
      {
        key: "compaction.stalenessDays",
        label: "Staleness (days)",
        hint: "Journal entries older than this are compaction candidates.",
        min: 0,
        max: 3650,
        step: 1,
        integer: false,
      },
      {
        key: "compaction.minClusterSize",
        label: "Min cluster size",
        hint: "Smallest cluster that gets compacted into a fact.",
        min: 2,
        max: 1000,
        step: 1,
        integer: true,
      },
    ],
  },
];
