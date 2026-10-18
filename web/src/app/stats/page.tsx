import type { Metadata } from "next";
import { StatsPage } from "@/views/stats/stats-page";

export const metadata: Metadata = { title: "Stats" };

export default function StatsRoute() {
  return <StatsPage />;
}
