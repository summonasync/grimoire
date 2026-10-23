"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { shelfKeys } from "@/features/shelf";
import { statsKeys } from "@/features/stats";
import { createMemory, forgetMemory, patchMemory, restoreMemory } from "../api";
import { memoryKeys } from "../queries";
import type {
  CreateMemoryInput,
  ForgetMemoryInput,
  PatchMemoryInput,
  RestoreMemoryInput,
} from "../types";

function useInvalidateMemories() {
  const queryClient = useQueryClient();
  return (id: string) => {
    void queryClient.invalidateQueries({ queryKey: memoryKeys.lists() });
    void queryClient.invalidateQueries({ queryKey: memoryKeys.detail(id) });
    void queryClient.invalidateQueries({ queryKey: memoryKeys.history(id) });
    void queryClient.invalidateQueries({ queryKey: shelfKeys.census() });
    void queryClient.invalidateQueries({ queryKey: statsKeys.all });
  };
}

export function useCreateMemory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateMemoryInput) => createMemory(input),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: memoryKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: memoryKeys.all });
      void queryClient.invalidateQueries({ queryKey: shelfKeys.census() });
      void queryClient.invalidateQueries({ queryKey: statsKeys.all });
    },
  });
}

export function usePatchMemory(id: string) {
  const invalidate = useInvalidateMemories();
  return useMutation({
    mutationFn: (input: PatchMemoryInput) => patchMemory(id, input),
    onSuccess: (updated) => invalidate(updated.id),
  });
}

export function useForgetMemory(id: string) {
  const invalidate = useInvalidateMemories();
  return useMutation({
    mutationFn: (input: ForgetMemoryInput) => forgetMemory(id, input.reason, input.tome),
    onSuccess: (forgotten) => invalidate(forgotten.id),
  });
}

export function useRestoreMemory(id: string) {
  const invalidate = useInvalidateMemories();
  return useMutation({
    mutationFn: (input: RestoreMemoryInput) => restoreMemory(id, input),
    onSuccess: (result) => invalidate(result.record.id),
  });
}
