'use client';

import { useCallback, useMemo, useState } from 'react';
import { Button, Modal, ModalBody, ModalContent, ModalFooter, ModalHeader } from '@heroui/react';
import {
    generateAvatarUrl,
    AVATAR_FEATURES,
    type AvatarOptions,
} from '@/lib/avatar';

type Props = {
    value?: string | null;
    seed: string;
    onChange: (json: string) => void;
    children: (props: { avatarSrc: string; onOpen: () => void }) => React.ReactNode;
};

const BG_COLORS = [
    'b6e3f4', 'c0aede', 'd1d4f9', 'ffd5dc', 'ffdfbf',
    'f9e8d9', 'a3d9a5', 'f5c6aa', 'cce2cb', 'e2cfc4',
];

const COLOR_FEATURES = new Set(['skinColor']);

export default function AvatarPicker({ value, seed, onChange, children }: Props) {
    const [open, setOpen] = useState(false);

    const initial = useMemo<AvatarOptions>(() => {
        if (value) {
            try { return JSON.parse(value); } catch { /* ignore */ }
        }
        return {};
    }, [value]);

    const [features, setFeatures] = useState<Record<string, string | undefined>>(() => {
        const f: Record<string, string | undefined> = {};
        for (const [k, v] of Object.entries(initial)) {
            if (k === 'seed' || k === 'backgroundColor') continue;
            f[k] = Array.isArray(v) ? v[0] : (v as string | undefined);
        }
        return f;
    });
    const [bg, setBg] = useState<string | undefined>(() => {
        const v = initial.backgroundColor;
        return Array.isArray(v) ? v[0] : undefined;
    });

    const currentOptions = useMemo<AvatarOptions>(() => {
        const opts: AvatarOptions = { seed };
        for (const [k, v] of Object.entries(features)) {
            if (v !== undefined) opts[k] = [v];
        }
        if (bg) opts.backgroundColor = [bg];
        return opts;
    }, [seed, features, bg]);

    const previewSrc = useMemo(() => generateAvatarUrl(seed, currentOptions), [seed, currentOptions]);

    const displaySrc = useMemo(() => {
        if (value) {
            try { return generateAvatarUrl(seed, JSON.parse(value)); } catch { /* fall through */ }
        }
        return generateAvatarUrl(seed);
    }, [seed, value]);

    const setFeature = useCallback((key: string, val: string | undefined) => {
        setFeatures(prev => ({ ...prev, [key]: val }));
    }, []);

    const handleConfirm = useCallback(() => {
        onChange(JSON.stringify(currentOptions));
        setOpen(false);
    }, [currentOptions, onChange]);

    const handleRandomize = useCallback(() => {
        const f: Record<string, string | undefined> = {};
        for (const def of AVATAR_FEATURES) {
            f[def.key] = def.values[Math.floor(Math.random() * def.values.length)];
        }
        setFeatures(f);
        setBg(BG_COLORS[Math.floor(Math.random() * BG_COLORS.length)]);
    }, []);

    const handleOpen = useCallback(() => {
        const parsed: AvatarOptions = value ? (() => { try { return JSON.parse(value); } catch { return {}; } })() : {};
        const f: Record<string, string | undefined> = {};
        for (const [k, v] of Object.entries(parsed)) {
            if (k === 'seed' || k === 'backgroundColor') continue;
            f[k] = Array.isArray(v) ? v[0] : (v as string | undefined);
        }
        setFeatures(f);
        const bgVal = parsed.backgroundColor;
        setBg(Array.isArray(bgVal) ? bgVal[0] : undefined);
        setOpen(true);
    }, [value]);

    return (
        <>
            {children({ avatarSrc: displaySrc, onOpen: handleOpen })}

            <Modal
                isOpen={open}
                onOpenChange={setOpen}
                size="3xl"
                scrollBehavior="inside"
                classNames={{
                    base: 'bg-content2 max-h-[90vh]',
                    header: 'border-b border-content3',
                    footer: 'border-t border-content3',
                    body: 'p-0',
                }}
            >
                <ModalContent>
                    <ModalHeader>Choose Your Avatar</ModalHeader>
                    <ModalBody>
                        {/* ── Pinned preview ── */}
                        <div className="sticky top-0 z-10 bg-content2 border-b border-content3 px-5 py-4 flex items-center gap-4">
                            <img
                                src={previewSrc}
                                alt="Avatar preview"
                                className="h-24 w-24 rounded-full ring-3 ring-primary/40 shrink-0"
                            />
                            <div className="flex flex-col gap-2">
                                <p className="text-sm text-foreground-500">
                                    Customize your look below or let fate decide.
                                </p>
                                <Button variant="flat" size="sm" className="w-fit" onPress={handleRandomize}>
                                    🎲 Randomize
                                </Button>
                            </div>
                        </div>

                        {/* ── Feature sections ── */}
                        <div className="px-5 py-4 flex flex-col gap-5">
                            {AVATAR_FEATURES.map(def => {
                                const isColor = COLOR_FEATURES.has(def.key);
                                const selected = features[def.key];

                                return (
                                    <div key={def.key}>
                                        <h3 className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-2">
                                            {def.label}
                                        </h3>
                                        <div className="flex flex-wrap gap-2">
                                            {isColor ? (
                                                <>
                                                    {def.values.map(v => (
                                                        <button
                                                            key={v}
                                                            type="button"
                                                            onClick={() => setFeature(def.key, v)}
                                                            className={`h-9 w-9 rounded-full ring-2 transition-all ${
                                                                selected === v
                                                                    ? 'ring-primary scale-110'
                                                                    : 'ring-transparent hover:ring-foreground-300'
                                                            }`}
                                                            style={{ backgroundColor: `#${v}` }}
                                                        />
                                                    ))}
                                                    <button
                                                        type="button"
                                                        onClick={() => setFeature(def.key, undefined)}
                                                        className={`h-9 w-9 rounded-full ring-2 transition-all flex items-center justify-center text-xs
                                                            ${selected === undefined ? 'ring-primary scale-110' : 'ring-transparent hover:ring-foreground-300'}
                                                            bg-content3 text-foreground-400`}
                                                    >
                                                        ✕
                                                    </button>
                                                </>
                                            ) : (
                                                <>
                                                    {def.values.map(v => (
                                                        <button
                                                            key={v}
                                                            type="button"
                                                            onClick={() => setFeature(def.key, v)}
                                                            className={`rounded-xl overflow-hidden ring-2 transition-all ${
                                                                selected === v
                                                                    ? 'ring-primary scale-110'
                                                                    : 'ring-transparent hover:ring-foreground-300'
                                                            }`}
                                                        >
                                                            <img
                                                                src={generateAvatarUrl(seed, { ...currentOptions, [def.key]: [v] })}
                                                                alt={v}
                                                                className="h-12 w-12"
                                                            />
                                                        </button>
                                                    ))}
                                                    <button
                                                        type="button"
                                                        onClick={() => setFeature(def.key, undefined)}
                                                        className={`h-12 w-12 rounded-xl ring-2 transition-all flex items-center justify-center text-xs
                                                            ${selected === undefined ? 'ring-primary scale-110' : 'ring-transparent hover:ring-foreground-300'}
                                                            bg-content3 text-foreground-400`}
                                                    >
                                                        ✕
                                                    </button>
                                                </>
                                            )}
                                        </div>
                                    </div>
                                );
                            })}

                            {/* ── Background colour ── */}
                            <div>
                                <h3 className="text-xs font-semibold uppercase tracking-wide text-foreground-400 mb-2">Background</h3>
                                <div className="flex flex-wrap gap-2">
                                    {BG_COLORS.map(c => (
                                        <button
                                            key={c}
                                            type="button"
                                            onClick={() => setBg(c)}
                                            className={`h-9 w-9 rounded-full ring-2 transition-all ${
                                                bg === c
                                                    ? 'ring-primary scale-110'
                                                    : 'ring-transparent hover:ring-foreground-300'
                                            }`}
                                            style={{ backgroundColor: `#${c}` }}
                                        />
                                    ))}
                                    <button
                                        type="button"
                                        onClick={() => setBg(undefined)}
                                        className={`h-9 w-9 rounded-full ring-2 transition-all flex items-center justify-center text-xs
                                            ${bg === undefined
                                                ? 'ring-primary scale-110'
                                                : 'ring-transparent hover:ring-foreground-300'
                                            } bg-content3 text-foreground-400`}
                                    >
                                        ✕
                                    </button>
                                </div>
                            </div>
                        </div>
                    </ModalBody>
                    <ModalFooter>
                        <Button variant="flat" onPress={() => setOpen(false)}>Cancel</Button>
                        <Button color="primary" onPress={handleConfirm}>Save Avatar</Button>
                    </ModalFooter>
                </ModalContent>
            </Modal>
        </>
    );
}
