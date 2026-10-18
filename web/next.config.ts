import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "export",
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
