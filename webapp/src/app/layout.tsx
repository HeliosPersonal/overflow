import type { Metadata } from "next";
import "./globals.css";
import Providers from "@/components/Providers";

export const metadata: Metadata = {
  title: "Overflow",
  description: "Overflow",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html suppressHydrationWarning lang="en" className="h-full">
      <body className="flex flex-col border-neutral-200 dark:border-neutral-800 dark:bg-default-50 h-full">
        <Providers>
          {children}
        </Providers>
      </body>
    </html>
  );
}
