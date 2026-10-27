"use client";

import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";
import { edgeClassOrder, edgeClassStyles, kindColorVars } from "../lib/graph-styles";
import { ALL_PROJECTS, NO_PROJECT } from "../lib/graph-view-model";
import type { RecordEdgeClass } from "../types";

interface GraphFilterBarProps {
  classes: Record<RecordEdgeClass, boolean>;
  onToggleClass: (edgeClass: RecordEdgeClass) => void;
  project: string;
  onProjectChange: (project: string) => void;
  projects: string[];
  hasUnprojected: boolean;
}

const kindLabels = {
  journal: "journal",
  fact: "fact",
} as const;

export function GraphFilterBar({
  classes,
  onToggleClass,
  project,
  onProjectChange,
  projects,
  hasUnprojected,
}: GraphFilterBarProps) {
  return (
    <section
      aria-label="Graph filters"
      className="panel mb-4 flex flex-wrap items-center gap-x-4 gap-y-2 px-3 py-2.5"
    >
      <fieldset className="flex flex-wrap items-center gap-1.5">
        <legend className="sr-only">Edge classes</legend>
        <span className="text-[0.68rem] tracking-wide text-muted uppercase">Edges</span>
        {edgeClassOrder.map((edgeClass) => {
          const style = edgeClassStyles[edgeClass];
          const on = classes[edgeClass];
          return (
            <button
              key={edgeClass}
              type="button"
              aria-pressed={on}
              onClick={() => onToggleClass(edgeClass)}
              className={cn(
                "flex items-center gap-1.5 rounded-md border px-2 py-1 text-[0.7rem] font-medium transition-colors",
                on
                  ? "border-jade/55 bg-jade/10 text-foreground"
                  : "border-border text-muted hover:border-jade/40 hover:text-foreground",
              )}
            >
              <span
                aria-hidden
                className={cn("h-0.5 w-3 rounded-full", !on && "opacity-40")}
                style={{ backgroundColor: `var(${style.colorVar})` }}
              />
              {style.label}
            </button>
          );
        })}
        <span
          title="Hollow dots are superseded or tombstoned endpoints of shown edges"
          className="flex items-center gap-1.5 rounded-md border border-dashed border-border px-2 py-1 text-[0.7rem] text-muted"
        >
          <span
            aria-hidden
            className="inline-block size-2 rounded-full border border-muted bg-transparent"
          />
          inactive
        </span>
      </fieldset>

      <label className="flex items-center gap-1.5 text-xs text-muted">
        <span className="whitespace-nowrap">Project</span>
        <span className="relative">
          <select
            value={project}
            onChange={(event) => onProjectChange(event.target.value)}
            aria-label="Filter by project"
            className="field h-8 appearance-none py-0 pr-7 pl-2.5 text-xs"
          >
            <option value={ALL_PROJECTS}>All</option>
            {hasUnprojected && <option value={NO_PROJECT}>no project</option>}
            {projects.map((entry) => (
              <option key={entry} value={entry}>
                {entry}
              </option>
            ))}
          </select>
          <Icon
            name="chevronDown"
            className="pointer-events-none absolute top-1/2 right-2 size-3 -translate-y-1/2 text-muted"
          />
        </span>
      </label>

      <span className="ml-auto flex items-center gap-3 text-[0.68rem] text-muted">
        {(Object.keys(kindColorVars) as Array<keyof typeof kindColorVars>).map((kind) => (
          <span key={kind} className="flex items-center gap-1.5">
            <span
              aria-hidden
              className="inline-block size-2.5 rounded-full border border-border-strong"
              style={{ backgroundColor: `var(${kindColorVars[kind]})` }}
            />
            {kindLabels[kind]}
          </span>
        ))}
      </span>
    </section>
  );
}
