import type { Metadata } from "next";
import { MemoriesPage } from "@/views/memories/memories-page";

export const metadata: Metadata = { title: "Memories" };

export default function MemoriesRoute() {
  return <MemoriesPage />;
}
