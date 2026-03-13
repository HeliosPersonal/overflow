import { createAvatar } from '@dicebear/core';
import { avataaars } from '@dicebear/collection';

/* ── Feature definitions ───────────────────────────────────────────── */

export type FeatureDef = { key: string; label: string; values: readonly string[] };

export const AVATAR_FEATURES: FeatureDef[] = [
    { key: 'eyes',           label: 'Eyes',         values: ['closed', 'cry', 'default', 'eyeRoll', 'happy', 'hearts', 'side', 'squint', 'surprised', 'winkWacky', 'wink', 'xDizzy'] },
    { key: 'eyebrows',       label: 'Eyebrows',     values: ['angryNatural', 'defaultNatural', 'flatNatural', 'frownNatural', 'raisedExcitedNatural', 'sadConcernedNatural', 'unibrowNatural', 'upDownNatural', 'angry', 'default', 'raisedExcited', 'sadConcerned', 'upDown'] },
    { key: 'mouth',          label: 'Mouth',        values: ['concerned', 'default', 'disbelief', 'eating', 'grimace', 'sad', 'screamOpen', 'serious', 'smile', 'tongue', 'twinkle', 'vomit'] },
    { key: 'top',            label: 'Hair / Hat',   values: ['bob', 'bun', 'curly', 'curvy', 'dreads', 'dreads01', 'dreads02', 'frida', 'fro', 'froBand', 'frizzle', 'longButNotTooLong', 'miaWallace', 'shaggy', 'shaggyMullet', 'shavedSides', 'shortCurly', 'shortFlat', 'shortRound', 'shortWaved', 'sides', 'straight01', 'straight02', 'straightAndStrand', 'theCaesar', 'theCaesarAndSidePart', 'bigHair', 'hat', 'hijab', 'turban', 'winterHat1', 'winterHat02', 'winterHat03', 'winterHat04'] },
    { key: 'accessories',    label: 'Accessories',  values: ['kurt', 'prescription01', 'prescription02', 'round', 'sunglasses', 'wayfarers', 'eyepatch'] },
    { key: 'clothing',       label: 'Clothing',     values: ['blazerAndShirt', 'blazerAndSweater', 'collarAndSweater', 'graphicShirt', 'hoodie', 'overall', 'shirtCrewNeck', 'shirtScoopNeck', 'shirtVNeck'] },
    { key: 'facialHair',     label: 'Facial Hair',  values: ['beardLight', 'beardMajestic', 'beardMedium', 'moustacheFancy', 'moustacheMagnum'] },
    { key: 'skinColor',      label: 'Skin',         values: ['614335', 'd08b5b', 'ae5d29', 'edb98a', 'ffdbb4', 'fd9841', 'f8d25c'] },
];

// Re-export for backwards compatibility (used by guest creation default avatar)
export const AVATAR_EYES = AVATAR_FEATURES[0].values;
export const AVATAR_MOUTH = AVATAR_FEATURES[2].values;

/* ── Options type ──────────────────────────────────────────────────── */

export type AvatarOptions = {
    seed?: string;
    [key: string]: string | string[] | undefined;
};

/* ── Generation ────────────────────────────────────────────────────── */

export function generateAvatarUrl(seed: string, options?: AvatarOptions): string {
    const opts: Record<string, unknown> = {
        seed: options?.seed ?? seed,
        radius: 50,
    };

    if (options) {
        for (const [k, v] of Object.entries(options)) {
            if (k === 'seed' || k === 'style') continue;
            if (v !== undefined) opts[k] = v;
        }
    }

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return createAvatar(avataaars as any, opts).toDataUri();
}

/* ── Parsing ───────────────────────────────────────────────────────── */

export function parseAvatarOptions(json: string | null | undefined): AvatarOptions | undefined {
    if (!json) return undefined;
    try {
        return JSON.parse(json) as AvatarOptions;
    } catch {
        return undefined;
    }
}
