"use client";

import { createContext, useContext, useMemo, useState, type ReactNode } from "react";

interface TomeFilterValue {
  tome: string | null;
  setTome: (tome: string | null) => void;
}

const TomeFilterContext = createContext<TomeFilterValue | null>(null);

export function TomeFilterProvider({ children }: { children: ReactNode }) {
  const [tome, setTome] = useState<string | null>(null);
  const value = useMemo(() => ({ tome, setTome }), [tome]);
  return <TomeFilterContext.Provider value={value}>{children}</TomeFilterContext.Provider>;
}

export function useTomeFilter(): TomeFilterValue {
  const value = useContext(TomeFilterContext);
  if (value === null) {
    throw new Error("useTomeFilter must be used inside TomeFilterProvider");
  }
  return value;
}
