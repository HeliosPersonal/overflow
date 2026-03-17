'use client';

import {AcademicCapIcon, TagIcon, TrophyIcon, Squares2X2Icon, ChevronDoubleRightIcon, ChevronDoubleLeftIcon} from "@heroicons/react/24/outline";
import {Tooltip} from "@heroui/react";
import {usePathname} from "next/navigation";
import Link from "next/link";

export default function SideMenu({ isAdmin = false, collapsed = false, onToggle }: {
    isAdmin?: boolean;
    collapsed?: boolean;
    onToggle?: () => void;
}) {
    const pathname = usePathname();

    const navLinks = [
        {key: 'poker', icon: Squares2X2Icon, text: 'Dashboard', href: '/'},
        {key: 'home', icon: AcademicCapIcon, text: 'Questions', href: '/questions'},
        ...(isAdmin ? [{key: 'tags', icon: TagIcon, text: 'Tags', href: '/tags'}] : []),
        {key: 'leaderboard', icon: TrophyIcon, text: 'Leaderboard', href: '/profiles'},
    ];

    const isActive = (href: string) => {
        if (href === '/') return pathname === '/' || pathname.startsWith('/planning-poker');
        if (href === '/profiles') return pathname === '/profiles';
        return pathname.startsWith(href);
    };

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
                            <Icon className="h-6 w-6 shrink-0"/>
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
                            <ChevronDoubleRightIcon className="h-5 w-5"/>
                        </button>
                    </Tooltip>
                ) : (
                    <button
                        onClick={onToggle}
                        className="flex items-center gap-2 rounded-xl px-3 py-2 text-foreground-400
                            hover:text-foreground-600 hover:bg-content2 transition-colors"
                    >
                        <ChevronDoubleLeftIcon className="h-5 w-5 shrink-0"/>
                        <span className="text-sm font-medium">Collapse</span>
                    </button>
                )
            )}
        </div>
    );
}