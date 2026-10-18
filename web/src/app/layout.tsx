import type { Metadata } from "next";
import type { ReactNode } from "react";
import { QueryProvider } from "@/shared/providers/query-provider";
import { AppShell } from "@/widgets/app-shell/app-shell";
import "./globals.css";

export const metadata: Metadata = {
  title: { default: "Grimoire", template: "%s · Grimoire" },
  description: "Local agent memory console.",
};

const themeBootstrap = `(function(){var t="dark";try{var s=localStorage.getItem("grimoire-theme");if(s==="light"||s==="dark")t=s;}catch(e){}document.documentElement.classList.toggle("dark",t==="dark");})();`;

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="bg-background text-foreground antialiased">
        <script dangerouslySetInnerHTML={{ __html: themeBootstrap }} />
        <QueryProvider>
          <AppShell>{children}</AppShell>
        </QueryProvider>
      </body>
    </html>
  );
}
