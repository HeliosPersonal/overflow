import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Overflow - Authentication",
  description: "Sign in or create your Overflow account",
};

export default function AuthLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return <>{children}</>;
}

