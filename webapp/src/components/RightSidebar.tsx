'use client';

import {usePathname} from "next/navigation";
import TrendingTags from "@/components/TrendingTags";
import TopUsers from "@/components/TopUsers";

/**
 * Right sidebar that hides itself on planning-poker pages
 * where the room needs full width.
 */
export default function RightSidebar() {
    const pathname = usePathname();
    const hide = pathname.startsWith('/planning-poker');

    if (hide) return null;

    return (
        <aside
            className='basis-1/5 shrink-0 px-6 pt-4 border-l border-neutral-200 dark:border-neutral-800 bg-white dark:bg-[#18181b] overflow-y-auto'>
            <div className='flex flex-col gap-6'>
                <TrendingTags/>
                <TopUsers/>
            </div>
        </aside>
    );
}

