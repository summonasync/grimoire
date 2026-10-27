"use client";

import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { Icon } from "@/shared/ui/icons";
import { PageHeader } from "@/shared/ui/page-header";
import {
  ALL_PROJECTS,
  NO_PROJECT,
  GraphCanvas,
  GraphFilterBar,
  buildGraphModel,
  resolveVisibility,
  tomeGraphQueryOptions,
  type RecordEdgeClass,
} from "@/features/graph";
import { handOffRecord } from "@/widgets/app-shell/record-handoff";
import { useTomeFilter } from "@/widgets/app-shell/tome-context";

const defaultClasses: Record<RecordEdgeClass, boolean> = {
  supersede: true,
  link: true,
  similarity: true,
  coMembership: false,
};

export function GraphPage() {
  const { tome } = useTomeFilter();
  const [classes, setClasses] = useState<Record<RecordEdgeClass, boolean>>(defaultClasses);
  const [project, setProject] = useState(ALL_PROJECTS);
  const [includeCoMembership, setIncludeCoMembership] = useState(false);

  const graph = useQuery({
    ...tomeGraphQueryOptions(tome ?? "", includeCoMembership),
    enabled: tome !== null,
  });

  const model = useMemo(() => (graph.data ? buildGraphModel(graph.data) : null), [graph.data]);
  const effectiveProject =
    project === ALL_PROJECTS || project === NO_PROJECT || model?.projects.includes(project)
      ? project
      : ALL_PROJECTS;
  const visibility = useMemo(
    () => (model ? resolveVisibility(model, { classes, project: effectiveProject }) : null),
    [model, classes, effectiveProject],
  );

  const toggleClass = (edgeClass: RecordEdgeClass) => {
    const next = !classes[edgeClass];
    setClasses({ ...classes, [edgeClass]: next });
    if (edgeClass === "coMembership") {
      setIncludeCoMembership(next);
    }
  };

  const openRecord = (id: string) => {
    handOffRecord(id);
    window.location.hash = "memories";
  };

  if (!tome) {
    return (
      <div className="space-y-5">
        <PageHeader
          title="Graph"
          description="The record web of one tome: supersede chains, links, similarity and co-membership."
        />
        <EmptyState
          icon="book"
          title="No tome is open"
          reason="The graph renders a single tome at a time — pick one on the shelf and follow its Graph link."
          action={{ label: "Open the shelf", href: "#shelf" }}
        />
      </div>
    );
  }

  if (graph.error) {
    return (
      <div className="space-y-5">
        <PageHeader title="Graph" description="The record web of one tome." />
        <QueryFailure message={graph.error.message} onRetry={() => void graph.refetch()} />
      </div>
    );
  }

  if (graph.isPending || !model || !visibility) {
    return (
      <div className="space-y-5">
        <PageHeader title="Graph" description="The record web of one tome." />
        <Skeleton className="h-[calc(100dvh-17rem)] min-h-[24rem]" />
      </div>
    );
  }

  if (graph.data.nodes.length === 0) {
    return (
      <div className="space-y-5">
        <PageHeader title="Graph" description="The record web of one tome." />
        <EmptyState
          icon="spark"
          title={`'${graph.data.tome}' holds no active records`}
          reason="Only active records get a node here — write a memory to this tome or restore an archived one."
          action={{ label: "Open memories", href: "#memories" }}
        />
      </div>
    );
  }

  return (
    <div className="flex h-full min-h-0 flex-col">
      <PageHeader
        title="Graph"
        description="Every active record of the tome, arranged by the links between them."
        actions={
          <>
            <a
              href="#shelf"
              className="flex items-center gap-1.5 rounded-lg border border-border-strong bg-surface/80 px-2.5 py-1.5 text-xs text-muted transition-colors hover:border-jade hover:text-jade"
            >
              <Icon name="arrowLeft" className="size-3.5" />
              Shelf
            </a>
            <span className="flex items-center gap-1.5 rounded-lg border border-border-strong bg-surface/80 px-2.5 py-1.5 text-xs">
              <Icon name="book" className="size-3.5 text-muted" />
              <span className="text-muted">Tome</span>
              <span className="font-medium">{graph.data.tome}</span>
            </span>
          </>
        }
      />

      <GraphFilterBar
        classes={classes}
        onToggleClass={toggleClass}
        project={effectiveProject}
        onProjectChange={setProject}
        projects={model.projects}
        hasUnprojected={model.hasUnprojected}
      />

      <section
        aria-label="Record graph"
        className="panel relative h-[calc(100dvh-17rem)] min-h-[24rem] overflow-hidden"
      >
        <GraphCanvas model={model} visibility={visibility} onOpenRecord={openRecord} />
        <p className="pointer-events-none absolute bottom-2 left-3 text-[0.68rem] text-muted">
          {visibility.nodeCount} record{visibility.nodeCount === 1 ? "" : "s"} ·{" "}
          {visibility.linkCount} edge{visibility.linkCount === 1 ? "" : "s"}
          {model.ghostCount > 0 && ` · ${model.ghostCount} inactive`}
        </p>
      </section>
    </div>
  );
}
