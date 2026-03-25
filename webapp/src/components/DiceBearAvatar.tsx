'use client';

import { Avatar } from '@heroui/avatar';
import { useMemo } from 'react';
import { generateAvatarUrl, parseAvatarOptions } from '@/lib/avatar';

type Props = {
    /** User ID — used as fallback seed when no avatarJson is set. */
    userId?: string;
    /** Persisted avatar options JSON from ProfileService. */
    avatarJson?: string | null;
    /** HeroUI Avatar size. */
    size?: 'sm' | 'md' | 'lg';
    /** Extra Tailwind classes applied to the root `<Avatar>`. */
    className?: string;
    /**
     * Single, continuous border around the avatar.
     * Pass any Tailwind `border-*` classes (e.g. `"border-2 border-primary"`).
     * Rendered as a real CSS border — no ring-offset gaps or background bleed.
     * Defaults to no border.
     */
    borderClass?: string;
    /** Fallback initial character (display name first letter). */
    name?: string;
};

/**
 * Drop-in replacement for `<Avatar>` that renders a DiceBear avataaars face.
 *
 * Uses a single CSS `border` for the colored ring — avoids HeroUI's `color`
 * prop (which sets `bg-primary` that bleeds through as a sub-pixel gap) and
 * Tailwind `ring`/`ring-offset` (which create multi-layered, gapped borders).
 *
 * Priority:
 * 1. If `avatarJson` is a valid options object → render customized avatar.
 * 2. If `userId` is provided → deterministic avatar from seed.
 * 3. Falls back to HeroUI default (initial letter via `name`).
 */
export default function DiceBearAvatar({
    userId,
    avatarJson,
    size,
    className,
    borderClass,
    name,
}: Props) {
    const src = useMemo(() => {
        const seed = userId ?? name ?? 'default';
        const opts = parseAvatarOptions(avatarJson);
        return generateAvatarUrl(seed, opts);
    }, [userId, avatarJson, name]);

    return (
        <Avatar
            src={src}
            size={size}
            className={className}
            classNames={{ base: `bg-default-200 ${borderClass ?? ''}` }}
            name={name}
            showFallback={false}
        />
    );
}
