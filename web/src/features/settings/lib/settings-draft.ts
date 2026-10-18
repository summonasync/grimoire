import type { SettingsResponse } from "@/shared/api/types";
import type { SettingsDraft, SettingsFieldKey, UpdateSettingsPayload } from "../types";
import { settingsFieldGroups, type SettingsFieldSpec } from "./field-groups";

const fieldKeys = settingsFieldGroups.flatMap((group) => group.fields.map((field) => field.key));

export function draftFromSettings(settings: SettingsResponse): SettingsDraft {
  return {
    sessionTtlHours: String(settings.memory.sessionTtlHours),
    dedupSimilarityThreshold: String(settings.memory.dedupSimilarityThreshold),
    tombstoneRetentionDays: String(settings.memory.tombstoneRetentionDays),
    recallOversampling: String(settings.memory.recallOversampling),
    "halfLives.sessionDays": String(settings.memory.recencyHalfLives.sessionDays),
    "halfLives.projectDays": String(settings.memory.recencyHalfLives.projectDays),
    "halfLives.globalDays": String(settings.memory.recencyHalfLives.globalDays),
    "halfLives.personalDays": String(settings.memory.recencyHalfLives.personalDays),
    "compaction.minSimilarity": String(settings.memory.compaction.minSimilarity),
    "compaction.stalenessDays": String(settings.memory.compaction.stalenessDays),
    "compaction.minClusterSize": String(settings.memory.compaction.minClusterSize),
  };
}

export function validateDraft(draft: SettingsDraft): Partial<Record<SettingsFieldKey, string>> {
  const errors: Partial<Record<SettingsFieldKey, string>> = {};
  for (const key of fieldKeys) {
    const spec = findSpec(key);
    const error = spec === null ? null : fieldError(draft[key], spec);
    if (error !== null) {
      errors[key] = error;
    }
  }
  return errors;
}

export function draftToPayload(draft: SettingsDraft): UpdateSettingsPayload {
  return {
    sessionTtlHours: Number(draft.sessionTtlHours),
    dedupSimilarityThreshold: Number(draft.dedupSimilarityThreshold),
    tombstoneRetentionDays: Number(draft.tombstoneRetentionDays),
    recallOversampling: Number(draft.recallOversampling),
    recencyHalfLives: {
      sessionDays: Number(draft["halfLives.sessionDays"]),
      projectDays: Number(draft["halfLives.projectDays"]),
      globalDays: Number(draft["halfLives.globalDays"]),
      personalDays: Number(draft["halfLives.personalDays"]),
    },
    compaction: {
      minSimilarity: Number(draft["compaction.minSimilarity"]),
      stalenessDays: Number(draft["compaction.stalenessDays"]),
      minClusterSize: Number(draft["compaction.minClusterSize"]),
    },
  };
}

export function draftsEqual(left: SettingsDraft, right: SettingsDraft): boolean {
  return fieldKeys.every((key) => left[key] === right[key]);
}

function findSpec(key: SettingsFieldKey): SettingsFieldSpec | null {
  for (const group of settingsFieldGroups) {
    const spec = group.fields.find((field) => field.key === key);
    if (spec !== undefined) {
      return spec;
    }
  }
  return null;
}

function fieldError(raw: string, spec: SettingsFieldSpec): string | null {
  if (raw.trim() === "") {
    return "Required.";
  }
  const value = Number(raw);
  if (Number.isNaN(value)) {
    return "Must be a number.";
  }
  if (spec.integer && !Number.isInteger(value)) {
    return "Must be a whole number.";
  }
  if (value < spec.min) {
    return `Must be ${spec.min} or more.`;
  }
  if (value > spec.max) {
    return `Must be ${spec.max} or less.`;
  }
  return null;
}
