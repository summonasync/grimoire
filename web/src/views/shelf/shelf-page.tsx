"use client";

import { useQuery } from "@tanstack/react-query";
import { tomesQueryOptions } from "@/features/shelf";
import { EmptyState, QueryFailure, Skeleton } from "@/shared/ui/honest";
import { PageHeader } from "@/shared/ui/page-header";
import { useTomeFilter } from "@/widgets/app-shell/tome-context";
import { TomeCard } from "./tome-card";

export function ShelfPage() {
  const shelf = useQuery(tomesQueryOptions());
  const { tome: picked, setTome } = useTomeFilter();

  return (
    <div className="space-y-5">
      <PageHeader
        title="Shelf"
        description="Every tome in the vault. Pick one and its memories open right away."
      />

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
          action={{
            label: "Open memories",
            onClick: () => {
              window.location.hash = "memories";
            },
          }}
        />
      ) : (
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          {shelf.data.tomes.map((summary) => (
            <TomeCard
              key={summary.key}
              summary={summary}
              picked={summary.key === picked}
              onPick={setTome}
            />
          ))}
        </div>
      )}
    </div>
  );
}
