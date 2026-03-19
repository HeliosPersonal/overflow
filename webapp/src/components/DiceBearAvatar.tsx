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
    /** HeroUI color ring. */
    color?: 'default' | 'primary' | 'secondary' | 'success' | 'warning' | 'danger';
    /** Fallback initial character (display name first letter). */
    name?: string;
};

/**
 * Drop-in replacement for `<Avatar>` that renders a DiceBear fun-emoji face.
 *
 * Priority:
 * 1. If `avatarJson` is a valid options object → render customized avatar.
 * 2. If `userId` is provided → deterministic avatar from seed.
 * 3. Falls back to HeroUI default (initial letter).
 */
export default function DiceBearAvatar({
    userId,
    avatarJson,
    size,
    className,
    color = 'primary',
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
            color={color}
            name={name}
            showFallback={false}
        />
    );
}

