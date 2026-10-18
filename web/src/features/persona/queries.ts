import { fetchPersona } from "./api";

export const personaKeys = {
  all: ["persona"] as const,
  document: () => [...personaKeys.all, "document"] as const,
};

export function personaQueryOptions() {
  return {
    queryKey: personaKeys.document(),
    queryFn: fetchPersona,
  };
}
