"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { memoryKeys } from "@/features/memories";
import { statsKeys } from "@/features/stats";
import {
  createTomeManifest,
  forgetTome,
  mergeTome,
  patchTomeManifest,
  purgeTome,
  renameTome,
  retomeRecords,
} from "../api";
import { shelfKeys } from "../queries";
import type {
  CreateTomeManifestInput,
  ForgetTomeInput,
  MergeTomeInput,
  PatchTomeManifestInput,
  RenameTomeInput,
  RetomeRecordsInput,
  TomePurgeInput,
} from "../types";

function useInvalidateTomeGraph() {
  const queryClient = useQueryClient();
  return () => {
    void queryClient.invalidateQueries({ queryKey: shelfKeys.census() });
    void queryClient.invalidateQueries({ queryKey: memoryKeys.lists() });
    void queryClient.invalidateQueries({ queryKey: statsKeys.all });
  };
}

export function useCreateTomeManifest() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: CreateTomeManifestInput) => createTomeManifest(input),
    onSuccess: () => invalidate(),
  });
}

export function usePatchTomeManifest() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: PatchTomeManifestInput) => patchTomeManifest(input),
    onSuccess: () => invalidate(),
    onError: () => invalidate(),
  });
}

export function usePurgeTome() {
  const invalidate = useInvalidateTomeGraph();
  return useMutation({
    mutationFn: (input: TomePurgeInput) => purgeTome(input),
    onSuccess: () => invalidate(),
    onError: () => invalidate(),
  });
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
