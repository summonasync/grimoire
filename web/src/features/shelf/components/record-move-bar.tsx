"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { tomesQueryOptions } from "../queries";
import { useShelfToasts } from "../lib/shelf-toasts";
import { useRetomeRecords } from "../lib/use-shelf-mutations";
import { Button } from "@/shared/ui/button";
import { Icon } from "@/shared/ui/icons";

interface RecordMoveBarProps {
  ids: string[];
  sourceTome: string | null;
  onDone: () => void;
}

export function RecordMoveBar({ ids, sourceTome, onDone }: RecordMoveBarProps) {
  const retome = useRetomeRecords();
  const { push } = useShelfToasts();
  const shelf = useQuery(tomesQueryOptions());
  const [target, setTarget] = useState("");

  if (ids.length === 0) {
    return null;
  }

  const targets = (shelf.data?.tomes ?? []).filter((tome) => tome.key !== sourceTome);

  const undoMove = (moved: string[], backTo: string) => {
    retome.mutate(
      { ids: moved, to: backTo },
      {
        onSuccess: (result) =>
          push({
            kind: "success",
            text: `Moved ${result.retomed} records back to '${result.to}'.`,
          }),
        onError: (error) => push({ kind: "error", text: error.message }),
      },
    );
  };

  const move = () => {
    if (target === "" || retome.isPending) {
      return;
    }
    retome.mutate(
      { ids, to: target },
      {
        onSuccess: (result) => {
          push({
            kind: "success",
            text: `Moved ${result.retomed} records to '${result.to}'${
              result.skipped > 0 ? ` — ${result.skipped} skipped` : ""
            }.`,
            action: sourceTome
              ? { label: "Undo", onClick: () => undoMove(ids, sourceTome) }
              : undefined,
          });
          setTarget("");
          onDone();
        },
        onError: (error) => push({ kind: "error", text: error.message }),
      },
    );
  };

  return (
    <div className="flex flex-wrap items-center gap-2 border-b border-border px-4 py-2">
      <Icon name="arrowRight" className="size-3.5 shrink-0 text-muted" />
      <p className="text-xs text-muted">
        {ids.length} selected
      </p>
      <select
        value={target}
        onChange={(event) => setTarget(event.target.value)}
        aria-label="Target tome"
        className="field h-8 w-auto py-0 text-xs"
      >
        <option value="" disabled>
          Move to tome…
        </option>
        {targets.map((tome) => (
          <option key={tome.key} value={tome.key}>
            {tome.key} ({tome.active})
          </option>
        ))}
      </select>
      <Button
        variant="outline"
        size="sm"
        icon="arrowRight"
        onClick={move}
        disabled={target === "" || retome.isPending}
      >
        {retome.isPending ? "Moving…" : "Move"}
      </Button>
      <button
        type="button"
        onClick={onDone}
        className="cursor-pointer text-xs text-muted transition-colors hover:text-foreground"
      >
        Clear selection
      </button>
      {retome.error && <p className="w-full text-xs text-ember">{retome.error.message}</p>}
    </div>
  );
}
