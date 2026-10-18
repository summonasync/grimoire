import { fetchSettings } from "./api";

export const settingsKeys = {
  all: ["settings"] as const,
};

export function settingsQueryOptions() {
  return {
    queryKey: settingsKeys.all,
    queryFn: fetchSettings,
  };
}
