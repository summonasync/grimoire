"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { TomeCreateForm, tomesQueryOptions } from "@/features/shelf";
import { Button } from "@/shared/ui/button";
import { Panel } from "@/shared/ui/panel";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { PageHeader } from "@/shared/ui/page-header";
import { useTomeFilter } from "@/widgets/app-shell/tome-context";
import { TomeCard } from "./tome-card";

export function ShelfPage() {
  const shelf = useQuery(tomesQueryOptions());
  const { tome: picked, setTome } = useTomeFilter();
  const [creating, setCreating] = useState(false);

  return (
    <div className="space-y-5">
      <PageHeader
        title="Shelf"
        description="Every tome in the vault. Pick one and its memories open right away."
        actions={
          <Button
            variant="primary"
            size="sm"
            icon={creating ? "close" : "plus"}
            onClick={() => setCreating((current) => !current)}
          >
            {creating ? "Cancel" : "New tome"}
          </Button>
        }
      />

      {creating && (
        <Panel title="New tome" icon="book">
          <TomeCreateForm onDone={() => setCreating(false)} onCancel={() => setCreating(false)} />
        </Panel>
      )}

      {shelf.error ? (
        <QueryFailure message={shelf.error.message} onRetry={() => void shelf.refetch()} />
      ) : shelf.isPending || !shelf.data ? (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {[0, 1, 2, 3, 4, 5].map((index) => (
            <Skeleton key={index} className="h-36" />
          ))}
        </div>
      ) : shelf.data.tomes.length === 0 ? (
        <EmptyState
          icon="book"
          title="The shelf is bare"
          reason="No tome holds a single record yet — the census appears once the first memory is written to the store."
          action={{ label: "Open memories", href: "#memories" }}
        />
      ) : (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {shelf.data.tomes.map((summary) => (
            <TomeCard
              key={summary.key}
              summary={summary}
              tomes={shelf.data.tomes}
              picked={summary.key === picked}
              onPick={setTome}
              onGone={() => setTome(summary.key === picked ? null : picked)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
