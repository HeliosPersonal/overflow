'use client';

import {useEffect, useState} from "react";

// ── CSS utility classes ──────────────────────────────────────────────────────

export const FULL_PAGE_CENTER = "min-h-full bg-content1 flex items-center justify-center";
export const ICON_SM = "h-3.5 w-3.5";
export const ICON_MD = "h-5 w-5";
export const SECTION_LABEL = "text-xs font-semibold uppercase tracking-wide text-foreground-400";

// ── Behaviour ────────────────────────────────────────────────────────────────

export const CONFETTI_DURATION_MS = 1500;
export const VOTER_AVATAR_PREVIEW_LIMIT = 3;
export const TITLE_MAX_LENGTH = 80;

// ── PokerTableScene layout ───────────────────────────────────────────────────
// Base dimensions define the "ideal" 900×620 design; the scene scales to fill
// whatever container it's placed in.

const NAME_LABEL_WIDTH = 96;
const BASE_SCENE_W = 900;
const BASE_SCENE_H = 620;
const BASE_CENTER_RX = 230;
const BASE_CENTER_RY = 140;
const BASE_CARD_W = 54;
const BASE_CARD_H = 74;
const BASE_AVATAR_SIZE = 44;
// Gap between the table ellipse edge and the inner edge of the avatar circle.
// The orbit ellipse is derived from the table ellipse + this gap + avatar radius.
const SEAT_GAP = 8;

export const SPECTATOR_CARD_W = 160;

export type SceneDimensions = ReturnType<typeof useSceneSize>;

/**
 * Measures the container and returns pixel sizes for every scene element.
 * Orbit radii scale independently (X with width, Y with height);
 * element sizes use the smaller scale to stay proportional.
 */
export function useSceneSize(containerRef: React.RefObject<HTMLDivElement | null>) {
    const [dims, setDims] = useState({w: 0, h: 0});

    useEffect(() => {
        if (!containerRef.current) return;
        const ro = new ResizeObserver(([entry]) => {
            const w = entry.contentRect.width;
            const h = entry.contentRect.height;
            if (w > 0 && h > 0) setDims({w, h});
        });
        ro.observe(containerRef.current);
        return () => ro.disconnect();
    }, [containerRef]);

    const {w, h} = dims;
    const sx = w > 0 ? Math.min(1, w / BASE_SCENE_W) : (typeof window !== 'undefined' ? Math.min(1, window.innerWidth / BASE_SCENE_W) : 0.5);
    const sy = h > 0 ? Math.min(1, h / BASE_SCENE_H) : sx;
    const s = Math.min(sx, sy);

    // Orbit ellipse = table ellipse + avatar radius + seat gap, so participants sit
    // exactly on the table perimeter. seatOffset uses the smaller scale (s) so that
    // avatar/card sizes and the orbit offset all scale together.
    const seatOffset = (BASE_AVATAR_SIZE / 2 + SEAT_GAP) * s;
    // cardInward: distance (along the radial direction) from avatar centre to card centre.
    // Placing this as avatarRadius + seatGap + cardHalfHeight puts the card's outer
    // face right at the table edge and the body inside the table surface.
    const cardInward = (BASE_AVATAR_SIZE / 2 + SEAT_GAP + BASE_CARD_H / 2) * s;

    return {
        centerRx: BASE_CENTER_RX * sx,
        centerRy: BASE_CENTER_RY * sy,
        orbitRx: BASE_CENTER_RX * sx + seatOffset,
        orbitRy: BASE_CENTER_RY * sy + seatOffset,
        cardW: BASE_CARD_W * s,
        cardH: BASE_CARD_H * s,
        avatarSize: BASE_AVATAR_SIZE * s,
        cardInward,
        nameLabelWidth: NAME_LABEL_WIDTH * s,
        scale: s,
    };
}

