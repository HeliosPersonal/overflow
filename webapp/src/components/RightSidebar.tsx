'use client';

import {usePathname} from "next/navigation";
import TrendingTags from "@/components/TrendingTags";
import TopUsers from "@/components/TopUsers";

/**
 * Right sidebar — only shows on questions, tags, and leaderboard list pages.
 */
export default function RightSidebar() {
    const pathname = usePathname();
    const show = pathname.startsWith('/questions') || pathname.startsWith('/tags') || pathname === '/profiles';

    if (!show) return null;

    return (
        <aside className='basis-1/5 shrink-0 max-w-[280px] px-4 pt-4 bg-content1 overflow-y-auto'>
            <div className='flex flex-col gap-5'>
                <TrendingTags/>
                <TopUsers/>
            </div>
        </aside>
    );
}

