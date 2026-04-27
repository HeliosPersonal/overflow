'use client';

import { Coffee } from 'lucide-react';
import { Button } from '@heroui/button';
import { Tooltip } from '@heroui/react';
import { BUY_ME_A_COFFEE_URL } from '@/lib/constants';

type Props = {
    /**
     * compact — small icon+text link, suitable for top nav or expanded sidebar.
     * prominent — card with a short description and a call-to-action link, suitable for sidebars.
     */
    variant?: 'compact' | 'prominent';
    /**
     * When true (sidebar is collapsed), renders only the icon inside a Tooltip.
     * Only relevant for the compact variant.
     */
    collapsed?: boolean;
};

const LINK_PROPS = {
    href: BUY_ME_A_COFFEE_URL,
    target: '_blank',
    rel: 'noopener noreferrer',
};

export default function SupportMeButton({ variant = 'compact', collapsed = false }: Props) {
    if (variant === 'prominent') {
        return (
            <div className="rounded-2xl bg-content2 border border-content3 shadow-raise-sm p-4 flex flex-col gap-3">
                <div className="flex items-center gap-2">
                    <span className="flex items-center justify-center w-7 h-7 rounded-lg bg-amber-100/60 dark:bg-amber-900/20">
                        <Coffee size={16} className="text-amber-500 dark:text-amber-400/80" />
                    </span>
                    <h3 className="text-sm font-semibold text-foreground tracking-wide">Support the project</h3>
                </div>
                <p className="text-xs text-foreground-500 leading-relaxed">
                    If this project helps you, you can support its development.
                </p>
                <Button
                    as="a"
                    {...LINK_PROPS}
                    size="sm"
                    variant="flat"
                    color="warning"
                    startContent={<Coffee size={14} />}
                    className="w-full font-medium"
                    aria-label="Buy me a coffee – support the project"
                >
                    Buy me a coffee
                </Button>
            </div>
        );
    }

    // compact variant
    const linkContent = (
        <a
            {...LINK_PROPS}
            className="flex items-center gap-3 rounded-xl px-3 py-2.5 transition-colors
                text-foreground-500 hover:bg-content2 hover:text-amber-500
                focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary
                dark:hover:text-amber-400"
            aria-label="Buy me a coffee – support the project"
        >
            <Coffee size={collapsed ? 24 : 22} className="shrink-0" />
            {!collapsed && (
                <span className="text-base font-medium">Support</span>
            )}
        </a>
    );

    if (collapsed) {
        return (
            <Tooltip content="Buy me a coffee" placement="right">
                {linkContent}
            </Tooltip>
        );
    }

    return linkContent;
}

