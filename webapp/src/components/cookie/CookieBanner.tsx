'use client';

import { useCookieConsentStore } from '@/lib/hooks/useCookieConsentStore';
import { useEffect, useState } from 'react';
import CookiePreferencesModal from './CookiePreferencesModal';

export default function CookieBanner() {
    const { consent, showBanner, acceptAll, rejectNonEssential, openPreferences, showPreferences } =
        useCookieConsentStore();

    // Avoid hydration mismatch — store is rehydrated from localStorage on mount
    const [mounted, setMounted] = useState(false);
    useEffect(() => setMounted(true), []);

    if (!mounted) return null;

    return (
        <>
            {showBanner && consent === null && (
                <div
                    role="dialog"
                    aria-label="Cookie consent"
                    className="fixed inset-x-0 bottom-0 z-[100] flex justify-center p-4 animate-in slide-in-from-bottom-4 duration-300"
                >
                    <div className="w-full max-w-3xl rounded-xl border border-neutral-200 bg-white/95 p-5 shadow-2xl backdrop-blur dark:border-neutral-700 dark:bg-neutral-900/95">
                        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                            <div className="flex-1 space-y-1">
                                <h3 className="text-sm font-semibold text-foreground">
                                    🍪 We use cookies
                                </h3>
                                <p className="text-xs leading-relaxed text-neutral-500 dark:text-neutral-400">
                                    We use cookies to improve your experience, remember your
                                    preferences, and understand how you use our platform. You can
                                    customize your choices below.
                                </p>
                            </div>

                            <div className="flex flex-shrink-0 flex-wrap items-center gap-2">
                                <button
                                    onClick={rejectNonEssential}
                                    className="rounded-lg border border-neutral-300 px-4 py-2 text-xs font-medium text-neutral-700 transition-colors hover:bg-neutral-100 dark:border-neutral-600 dark:text-neutral-300 dark:hover:bg-neutral-800"
                                >
                                    Reject All
                                </button>
                                <button
                                    onClick={openPreferences}
                                    className="rounded-lg border border-neutral-300 px-4 py-2 text-xs font-medium text-neutral-700 transition-colors hover:bg-neutral-100 dark:border-neutral-600 dark:text-neutral-300 dark:hover:bg-neutral-800"
                                >
                                    Customize
                                </button>
                                <button
                                    onClick={acceptAll}
                                    className="rounded-lg bg-primary px-4 py-2 text-xs font-medium text-white transition-colors hover:bg-primary-600"
                                >
                                    Accept All
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {showPreferences && <CookiePreferencesModal />}
        </>
    );
}

