import type { Metadata } from "next";
import "./globals.css";
import Providers from "@/components/Providers";
import BuildVersion from "@/components/BuildVersion";
import CookieBanner from "@/components/cookie/CookieBanner";
import CookieSettingsButton from "@/components/cookie/CookieSettingsButton";
import { auth } from "@/auth";

export const metadata: Metadata = {
  title: "Overflow",
  description: "Overflow",
  icons: {
    icon: "/favicon.svg",
  },
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const session = await auth();
  const isAdmin = session?.user?.roles?.includes('admin') ?? false;

  return (
    <html suppressHydrationWarning lang="en" className="h-full">
      <body className="flex flex-col h-full bg-background">
        <Providers>
          {children}
          <BuildVersion isAdmin={isAdmin} />
          <CookieBanner />
          <div className="fixed bottom-16 right-3 sm:bottom-4 sm:right-4 z-40">
            <CookieSettingsButton />
          </div>
        </Providers>
      </body>
    </html>
  );
}
