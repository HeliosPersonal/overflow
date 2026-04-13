import TopNav from "@/components/nav/TopNav";
import RightSidebar from "@/components/RightSidebar";
import LayoutShell from "@/components/LayoutShell";
import React from "react";

export default async function MainLayout({
                                       children,
                                   }: Readonly<{
    children: React.ReactNode;
}>) {
    return (
        <LayoutShell
            topNav={<TopNav/>}
            rightSidebar={<RightSidebar/>}
        >
            {children}
        </LayoutShell>
    );
}
