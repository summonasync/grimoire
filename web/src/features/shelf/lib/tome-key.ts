export const maxTomeKeyLength = 64;
export const maxTomeDescriptionLength = 280;

export function normalizeTomeKey(candidate: string): string | null {
  const normalized = candidate
    .trim()
    .toLowerCase()
    .replace(/[ _]/g, "-")
    .replace(/-{2,}/g, "-")
    .replace(/^-+|-+$/g, "");
  if (normalized.length === 0 || normalized.length > maxTomeKeyLength) {
    return null;
  }
  return /^[a-z0-9-]+$/.test(normalized) ? normalized : null;
}
