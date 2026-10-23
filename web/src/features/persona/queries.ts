import { fetchPersona } from "./api";

export const personaKeys = {
  all: ["persona"] as const,
  document: (tome: string | null) => [...personaKeys.all, "document", tome ?? "own"] as const,
};

export function personaQueryOptions(tome: string | null) {
  return {
    queryKey: personaKeys.document(tome),
    queryFn: () => fetchPersona(tome),
  };
}
