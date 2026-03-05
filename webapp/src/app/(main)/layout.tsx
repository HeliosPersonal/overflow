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
    <>
      <TopNav />
      <div className="flex grow overflow-auto">
        <aside className='basis-1/10 shrink-0 border-r border-neutral-200 dark:border-neutral-800 pt-20 sticky top-0 px-6'>
          <SideMenu />
        </aside>
        <main className='flex-1 pt-20 h-full'>
          {children}
        </main>
        <aside className='basis-1/4 shrink-0 px-6 pt-20 border-l border-neutral-200 dark:border-neutral-800 dark:bg-default-100 sticky top-0'>
          <div className='flex flex-col gap-6'>
            <TrendingTags />
            <TopUsers />
          </div>
        </aside>
      </div>
    </>
  );
}

