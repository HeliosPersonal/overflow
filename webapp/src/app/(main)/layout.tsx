import TopNav from "@/components/nav/TopNav";
import SideMenu from "@/components/SideMenu";
import RightSidebar from "@/components/RightSidebar";
import LayoutShell from "@/components/LayoutShell";
import React from "react";
import {auth} from "@/auth";

export default async function MainLayout({
                                       children,
                                   }: Readonly<{
    children: React.ReactNode;
}>) {
    const session = await auth();
    const isAdmin = session?.user?.roles?.includes('admin') ?? false;

    return (
        <LayoutShell
            topNav={<TopNav/>}
            sideMenu={<SideMenu isAdmin={isAdmin}/>}
            rightSidebar={<RightSidebar/>}
        >
            {children}
        </LayoutShell>
    );
}
