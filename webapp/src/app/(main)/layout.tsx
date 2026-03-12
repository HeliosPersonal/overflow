import TopNav from "@/components/nav/TopNav";
import SideMenu from "@/components/SideMenu";
import RightSidebar from "@/components/RightSidebar";
import React from "react";

export default function MainLayout({
                                       children,
                                   }: Readonly<{
    children: React.ReactNode;
}>) {
    return (
        <div className="flex flex-col h-full">
            <TopNav/>
            <div className="flex flex-1 overflow-hidden mt-16">
                <aside className='basis-1/10 shrink-0 border-r border-neutral-200 dark:border-neutral-800 px-6 py-4'>
                    <SideMenu/>
                </aside>
                <main className='flex-1 overflow-y-auto bg-white dark:bg-[#18181b]'>
                    {children}
                </main>
                <RightSidebar/>
            </div>
        </div>
    );
}
