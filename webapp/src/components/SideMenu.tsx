'use client';

import {GraduationCap, Tag, Trophy, Dices} from "@/components/animated-icons";
import {ChevronsRight, ChevronsLeft} from "lucide-react";
import {Tooltip} from "@heroui/react";
import {usePathname} from "next/navigation";
import Link from "next/link";
import {useEffect, useState} from "react";

export default function SideMenu({ isAdmin = false, collapsed = false, onToggle, mobile = false }: {
    isAdmin?: boolean;
    collapsed?: boolean;
    onToggle?: () => void;
    mobile?: boolean;
}) {
    const pathname = usePathname();
    // Defer active-state computation to the client to avoid SSR/hydration mismatch.
    // Server always renders all links as inactive; client updates after mount.
    const [activePath, setActivePath] = useState('');
    useEffect(() => {
        setActivePath(pathname);
    }, [pathname]);

    const navLinks = [
        {key: 'poker', icon: Dices, text: 'Planning Poker', href: '/'},
        {key: 'home', icon: GraduationCap, text: 'Questions', href: '/questions'},
        ...(isAdmin ? [{key: 'tags', icon: Tag, text: 'Tags', href: '/tags'}] : []),
        ...(isAdmin ? [{key: 'leaderboard', icon: Trophy, text: 'Leaderboard', href: '/profiles'}] : []),
    ];

    const isActive = (href: string) => {
        if (!activePath) return false;
        if (href === '/') return activePath === '/' || activePath.startsWith('/planning-poker');
        if (href === '/profiles') return activePath === '/profiles';
        return activePath.startsWith(href);
    };

    // Mobile bottom navigation bar
    if (mobile) {
        return (
            <nav className="flex items-center justify-around bg-content1 border-t border-content3/50 px-2 py-1.5 safe-area-bottom">
                {navLinks.map(({key, href, icon: Icon, text}) => {
                    const active = isActive(href);
                    return (
                        <Link
                            key={key}
                            href={href}
                            className={`flex flex-col items-center gap-0.5 px-3 py-1 rounded-lg transition-colors min-w-0
                                ${active
                                    ? 'text-primary'
                                    : 'text-foreground-400'}`}
                        >
                            <Icon size={22} className="shrink-0"/>
                            <span className="text-[10px] font-medium truncate">{text}</span>
                        </Link>
                    );
                })}
            </nav>
        );
    }

    return (
        <div className="flex flex-col h-full gap-2">
            <nav className="flex flex-col gap-1">
                {navLinks.map(({key, href, icon: Icon, text}) => {
                    const active = isActive(href);
                    const linkContent = (
                        <Link
                            key={key}
                            href={href}
                            className={`flex items-center gap-3 rounded-xl px-3 py-2.5 transition-colors
                                ${active
                                    ? 'bg-content3 shadow-raise-sm text-foreground-800'
                                    : 'hover:bg-content2 text-foreground-500'}
                                ${collapsed ? 'justify-center' : ''}`}
                        >
                            <Icon size={24} className="shrink-0"/>
                            {!collapsed && (
                                <span className="text-base font-medium">{text}</span>
                            )}
                        </Link>
                    );

                    if (collapsed) {
                        return (
                            <Tooltip key={key} content={text} placement="right">
                                {linkContent}
                            </Tooltip>
                        );
                    }
                    return linkContent;
                })}
            </nav>

            <div className="flex-1"/>

            {/* Toggle button */}
            {onToggle && (
                collapsed ? (
                    <Tooltip content="Expand" placement="right">
                        <button
                            onClick={onToggle}
                            className="flex items-center justify-center rounded-xl px-3 py-2 text-foreground-400
                                hover:text-foreground-600 hover:bg-content2 transition-colors"
                        >
                            <ChevronsRight className="h-5 w-5"/>
                        </button>
                    </Tooltip>
                ) : (
                    <button
                        onClick={onToggle}
                        className="flex items-center gap-2 rounded-xl px-3 py-2 text-foreground-400
                            hover:text-foreground-600 hover:bg-content2 transition-colors"
                    >
                        <ChevronsLeft className="h-5 w-5 shrink-0"/>
                        <span className="text-sm font-medium">Collapse</span>
                    </button>
                )
            )}
        </div>
    );
}