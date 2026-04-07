'use client';

import {usePathname} from "next/navigation";
import React, {useState} from "react";
import SideMenu from "@/components/SideMenu";

/**
 * Wraps the (main) layout content.
 * Global header is always visible (including poker rooms).
 * Persistent collapsible sidebar below the header on all pages.
 *
 * SideMenu is rendered directly here (not via cloneElement) so the
 * `collapsed` prop is always explicitly controlled — eliminating the
 * SSR/client hydration mismatch that the cloneElement pattern caused.
 */
export default function LayoutShell({
    topNav,
    rightSidebar,
    children,
    isAdmin,
}: {
    topNav: React.ReactNode;
    rightSidebar: React.ReactNode;
    children: React.ReactNode;
    isAdmin: boolean;
}) {
    const pathname = usePathname();
    const isPokerRoom = /^\/planning-poker\/[0-9a-f]{8}-/.test(pathname);
    const [collapsed, setCollapsed] = useState(true);

    const sidebarCollapsed = isPokerRoom || collapsed;

    return (
        <div className="flex flex-col h-full">
            {/* Header — always visible */}
            {topNav}

            <div className="flex flex-1 overflow-hidden mt-14">
                {/* ── Collapsible Sidebar (hidden on mobile, always collapsed in poker rooms) ── */}
                <aside
                    className={`hidden md:block shrink-0 bg-content1 border-r border-content3/50 py-4
                        transition-all duration-300 ease-out overflow-hidden
                        ${sidebarCollapsed ? 'w-[68px] px-2' : 'w-56 px-4'}`}
                >
                    <SideMenu
                        isAdmin={isAdmin}
                        collapsed={sidebarCollapsed}
                        onToggle={isPokerRoom ? undefined : () => setCollapsed(c => !c)}
                    />
                </aside>

                {/* ── Main content ── */}
                <main className={`flex-1 bg-background ${isPokerRoom ? 'overflow-hidden' : 'overflow-y-auto'}`}>
                    {children}
                </main>

                {/* ── Right sidebar (only on non-poker pages, hidden on mobile) ── */}
                {!isPokerRoom && (
                    <div className="hidden lg:contents">
                        {rightSidebar}
                    </div>
                )}
            </div>

            {/* ── Mobile bottom navigation ── */}
            <div className="md:hidden shrink-0">
                <SideMenu isAdmin={isAdmin} collapsed={true} mobile={true} />
            </div>
        </div>
    );
}
