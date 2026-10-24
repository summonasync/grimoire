import { execSync } from "node:child_process";
import type { NextConfig } from "next";

function webSha(): string {
  try {
    return execSync("git rev-parse --short HEAD", { stdio: ["ignore", "pipe", "ignore"] })
      .toString()
      .trim();
  } catch {
    return "dev";
  }
}

const nextConfig: NextConfig = {
  output: "export",
  images: { unoptimized: true },
  env: {
    NEXT_PUBLIC_WEB_SHA: webSha(),
  },
  async rewrites() {
    if (process.env.NODE_ENV !== "development") {
      return [];
    }
    const target = process.env.GRIMOIRE_API ?? "http://127.0.0.1:4770";
    return [
      { source: "/api/:path*", destination: `${target}/api/:path*` },
      { source: "/scry", destination: `${target}/scry` },
      { source: "/health", destination: `${target}/health` },
    ];
  },
};

export default nextConfig;
