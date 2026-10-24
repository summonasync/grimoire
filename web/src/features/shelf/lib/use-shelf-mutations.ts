"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { memoryKeys } from "@/features/memories";
import { statsKeys } from "@/features/stats";
import { forgetTome, mergeTome, renameTome, retomeRecords } from "../api";
import { shelfKeys } from "../queries";
import type {
  ForgetTomeInput,
  MergeTomeInput,
  RenameTomeInput,
  RetomeRecordsInput,
} from "../types";

function useInvalidateTomeGraph() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: shelfKeys.census() });
    void queryClient.invalidateQueries({ queryKey: memoryKeys.lists() });
    void queryClient.invalidateQueries({ queryKey: statsKeys.all });
  };
}

export function useRenameTome() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: RenameTomeInput) => renameTome(input),
    onSuccess: () => invalidate(),
  });
}

export function useMergeTome() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: MergeTomeInput) => mergeTome(input),
    onSuccess: () => invalidate(),
  });
}

export function useRetomeRecords() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: RetomeRecordsInput) => retomeRecords(input),
    onSuccess: () => invalidate(),
  });
}

export function useForgetTome() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: ForgetTomeInput) => forgetTome(input),
    onSuccess: () => invalidate(),
  });
}
