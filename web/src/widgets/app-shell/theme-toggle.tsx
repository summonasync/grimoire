"use client";

const themeStorageKey = "grimoire-theme";

type Theme = "dark" | "light";

function toggleTheme(): void {
  const dark = document.documentElement.classList.toggle("dark");
  persistTheme(dark ? "dark" : "light");
}

function persistTheme(theme: Theme): void {
  try {
    localStorage.setItem(themeStorageKey, theme);
  } catch {
    return;
  }
}

export function ThemeToggle() {
  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-label="Toggle color theme"
      className="rounded-md border border-border p-2 text-muted transition-colors hover:text-foreground"
    >
      <svg
        aria-hidden="true"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="hidden size-4 dark:block"
      >
        <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8Z" />
      </svg>
      <svg
        aria-hidden="true"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="size-4 dark:hidden"
      >
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2m0 16v2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M2 12h2m16 0h2M4.9 19.1l1.4-1.4m11.4-11.4 1.4-1.4" />
      </svg>
    </button>
  );
}
