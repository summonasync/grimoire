import type { ReactNode } from "react";

export type IconName =
  | "console"
  | "memories"
  | "persona"
  | "stats"
  | "settings"
  | "scry"
  | "search"
  | "plus"
  | "edit"
  | "tombstone"
  | "restore"
  | "close"
  | "check"
  | "alert"
  | "clock"
  | "trash"
  | "save"
  | "filter"
  | "chevronDown"
  | "chevronRight"
  | "arrowLeft"
  | "arrowRight"
  | "link"
  | "tag"
  | "star"
  | "book"
  | "shelf"
  | "pulse"
  | "spark"
  | "database"
  | "menu"
  | "external";

const paths: Record<IconName, ReactNode> = {
  console: (
    <>
      <path d="M4 6h16v12H4z" />
      <path d="M4 10h16" />
    </>
  ),
  memories: (
    <>
      <path d="M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3z" />
      <path d="M8 8h8" />
      <path d="M8 12h8" />
    </>
  ),
  persona: (
    <>
      <circle cx="12" cy="8" r="3.5" />
      <path d="M5 20c1.2-3.6 3.8-5.4 7-5.4s5.8 1.8 7 5.4" />
    </>
  ),
  stats: (
    <>
      <path d="M4 20h16" />
      <path d="M7 20v-7" />
      <path d="M12 20V6" />
      <path d="M17 20v-4" />
    </>
  ),
  settings: (
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 3v2.5M12 18.5V21M3 12h2.5M18.5 12H21M5.6 5.6l1.8 1.8M16.6 16.6l1.8 1.8M18.4 5.6l-1.8 1.8M7.4 16.6l-1.8 1.8" />
    </>
  ),
  scry: (
    <>
      <path d="M2.5 12S6 6 12 6s9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6z" />
      <circle cx="12" cy="12" r="2.5" />
    </>
  ),
  search: (
    <>
      <circle cx="11" cy="11" r="6" />
      <path d="M15.6 15.6L20 20" />
    </>
  ),
  plus: (
    <>
      <path d="M12 5v14" />
      <path d="M5 12h14" />
    </>
  ),
  edit: (
    <>
      <path d="M4 20h4l10-10-4-4L4 16z" />
      <path d="M14.5 5.5l4 4" />
    </>
  ),
  tombstone: (
    <>
      <path d="M7 20V9a5 5 0 0 1 10 0v11z" />
      <path d="M9.5 12.5h5" />
      <path d="M12 10v5" />
    </>
  ),
  restore: (
    <>
      <path d="M4 12a8 8 0 1 0 2.6-5.9" />
      <path d="M4 4v4h4" />
    </>
  ),
  close: (
    <>
      <path d="M6 6l12 12" />
      <path d="M18 6L6 18" />
    </>
  ),
  check: <path d="M4.5 12.5l5 5L20 7" />,
  alert: (
    <>
      <path d="M12 4l9 16H3z" />
      <path d="M12 10v4" />
      <path d="M12 17h.01" />
    </>
  ),
  clock: (
    <>
      <circle cx="12" cy="12" r="8" />
      <path d="M12 8v4.5l3 1.8" />
    </>
  ),
  trash: (
    <>
      <path d="M4 7h16" />
      <path d="M7 7l1 13h8l1-13" />
      <path d="M10 4h4" />
    </>
  ),
  save: (
    <>
      <path d="M5 5h11l3 3v11H5z" />
      <path d="M8 5v5h7V5" />
      <path d="M8 15h8" />
    </>
  ),
  filter: <path d="M4 5h16l-6 7v6l-4 2v-8z" />,
  chevronDown: <path d="M6 9.5l6 6 6-6" />,
  chevronRight: <path d="M9.5 6l6 6-6 6" />,
  arrowLeft: (
    <>
      <path d="M19 12H5" />
      <path d="M11 6l-6 6 6 6" />
    </>
  ),
  arrowRight: (
    <>
      <path d="M5 12h14" />
      <path d="M13 6l6 6-6 6" />
    </>
  ),
  link: (
    <>
      <path d="M10 14a4 4 0 0 0 5.7 0l2.6-2.6a4 4 0 1 0-5.7-5.7L11.4 7" />
      <path d="M14 10a4 4 0 0 0-5.7 0L5.7 12.6a4 4 0 0 0 5.7 5.7L13 17" />
    </>
  ),
  tag: (
    <>
      <path d="M4 4h7l9 9-7 7-9-9z" />
      <circle cx="8.5" cy="8.5" r="1.4" />
    </>
  ),
  star: <path d="M12 4l2.5 5.2 5.5.8-4 3.9 1 5.6-5-2.8-5 2.8 1-5.6-4-3.9 5.5-.8z" />,
  book: (
    <>
      <path d="M4 5h7a2 2 0 0 1 2 2v12a2 2 0 0 0-2-2H4z" />
      <path d="M20 5h-7a2 2 0 0 0-2 2v12a2 2 0 0 1 2-2h7z" />
    </>
  ),
  shelf: (
    <>
      <path d="M4 4h16v16H4z" />
      <path d="M4 11h16" />
      <path d="M8 14.5V20M12 14.5V20M16 14.5V20" />
    </>
  ),
  pulse: <path d="M2 12h4l3-7 4 14 3-7h6" />,
  spark: (
    <>
      <path d="M12 3v5" />
      <path d="M12 16v5" />
      <path d="M3 12h5" />
      <path d="M16 12h5" />
      <path d="M6.5 6.5l3 3" />
      <path d="M14.5 14.5l3 3" />
      <path d="M17.5 6.5l-3 3" />
      <path d="M9.5 14.5l-3 3" />
    </>
  ),
  database: (
    <>
      <ellipse cx="12" cy="6" rx="7" ry="3" />
      <path d="M5 6v12c0 1.7 3.1 3 7 3s7-1.3 7-3V6" />
      <path d="M5 12c0 1.7 3.1 3 7 3s7-1.3 7-3" />
    </>
  ),
  menu: (
    <>
      <path d="M5.5 12h.01" />
      <path d="M12 12h.01" />
      <path d="M18.5 12h.01" />
    </>
  ),
  external: (
    <>
      <path d="M14 4h6v6" />
      <path d="M20 4l-9 9" />
      <path d="M18 13v7H4V6h7" />
    </>
  ),
};

interface IconProps {
  name: IconName;
  className?: string;
  strokeWidth?: number;
}

export function Icon({ name, className = "size-4", strokeWidth = 1.6 }: IconProps) {
  return (
    <svg
      aria-hidden
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      {paths[name]}
    </svg>
  );
}
