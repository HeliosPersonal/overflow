import type { Metadata } from "next";
import "./globals.css";
import Providers from "@/components/Providers";
import BuildVersion from "@/components/BuildVersion";
import CookieBanner from "@/components/cookie/CookieBanner";
import CookieSettingsButton from "@/components/cookie/CookieSettingsButton";

export const metadata: Metadata = {
  title: "Overflow",
  description: "Overflow",
  icons: {
    icon: "/favicon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html suppressHydrationWarning lang="en" className="h-full">
      <body className="flex flex-col h-full bg-background">
        <Providers>
          {children}
          <BuildVersion />
          <CookieBanner />
          <div className="fixed bottom-4 right-4 z-50">
            <CookieSettingsButton />
          </div>
        </Providers>
      </body>
    </html>
  );
}
