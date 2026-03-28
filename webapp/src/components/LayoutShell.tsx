'use client';

import {usePathname} from "next/navigation";
import React, {useState} from "react";

/**
 * Wraps the (main) layout content.
 * Global header is always visible (including poker rooms).
 * Persistent collapsible sidebar below the header on all pages.
 */
export default function LayoutShell({
    topNav,
    sideMenu,
    rightSidebar,
    children,
}: {
    topNav: React.ReactNode;
    sideMenu: React.ReactNode;
    rightSidebar: React.ReactNode;
    children: React.ReactNode;
}) {
    const pathname = usePathname();
    const isPokerRoom = /^\/planning-poker\/[0-9a-f]{8}-/.test(pathname);
    const [collapsed, setCollapsed] = useState(true);

    return (
        <div className="flex flex-col h-full">
            {/* Header — always visible */}
            {topNav}

            <div className="flex flex-1 overflow-hidden mt-14">
                {/* ── Collapsible Sidebar (hidden in poker rooms + mobile) ── */}
                {!isPokerRoom && (
                    <aside
                        className={`hidden md:block shrink-0 bg-content1 border-r border-content3/50 py-4
                            transition-all duration-300 ease-out overflow-hidden
                            ${collapsed ? 'w-[68px] px-2' : 'w-56 px-4'}`}
                    >
                        {React.isValidElement<{ collapsed?: boolean; onToggle?: () => void }>(sideMenu)
                            ? React.cloneElement(sideMenu, { collapsed, onToggle: () => setCollapsed(c => !c) })
                            : sideMenu}
                    </aside>
                )}

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

            {/* ── Mobile bottom navigation (hidden in poker rooms) ── */}
            {!isPokerRoom && (
                <div className="md:hidden shrink-0">
                    {React.isValidElement<{ collapsed?: boolean; mobile?: boolean }>(sideMenu)
                        ? React.cloneElement(sideMenu, { collapsed: true, mobile: true })
                        : null}
                </div>
            )}
        </div>
    );
}
