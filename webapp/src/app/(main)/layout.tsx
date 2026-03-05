import TopNav from "@/components/nav/TopNav";
import SideMenu from "@/components/SideMenu";
import TrendingTags from "@/components/TrendingTags";
import TopUsers from "@/components/TopUsers";
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
                <aside
                    className='basis-1/5 shrink-0 px-6 pt-4 border-l border-neutral-200 dark:border-neutral-800 bg-white dark:bg-[#18181b] overflow-y-auto'>
                    <div className='flex flex-col gap-6'>
                        <TrendingTags/>
                        <TopUsers/>
                    </div>
                </aside>
            </div>
        </div>
    );
}
