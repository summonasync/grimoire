"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { createMemory, forgetMemory, patchMemory, restoreMemory } from "../api";
import { memoryKeys } from "../queries";
import type { CreateMemoryInput, PatchMemoryInput, RestoreMemoryInput } from "../types";

function useInvalidateMemories() {
  const queryClient = useQueryClient();
  return (id: string) => {
    void queryClient.invalidateQueries({ queryKey: memoryKeys.lists() });
    void queryClient.invalidateQueries({ queryKey: memoryKeys.detail(id) });
    void queryClient.invalidateQueries({ queryKey: memoryKeys.history(id) });
  };
}

export function useCreateMemory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: CreateMemoryInput) => createMemory(input),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: memoryKeys.lists() });
      void queryClient.invalidateQueries({ queryKey: memoryKeys.all });
      void result;
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
    mutationFn: (reason: string) => forgetMemory(id, reason),
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
