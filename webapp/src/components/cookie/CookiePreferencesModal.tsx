'use client';

import { useState } from 'react';
import { useCookieConsentStore, type CookieConsent } from '@/lib/hooks/useCookieConsentStore';

const categories: {
    key: keyof Omit<CookieConsent, 'necessary'>;
    label: string;
    description: string;
}[] = [
    {
        key: 'functional',
        label: 'Functional Cookies',
        description:
            'Enable features like theme preference, cached tags, and editor settings. Disabling these may degrade your experience.',
    },
    {
        key: 'analytics',
        label: 'Analytics Cookies',
        description:
            'Help us understand how the platform is used so we can improve performance and features. Data is anonymised.',
    },
];

export default function CookiePreferencesModal() {
    const { consent, savePreferences, acceptAll, closePreferences } = useCookieConsentStore();

    const [prefs, setPrefs] = useState({
        functional: consent?.functional ?? false,
        analytics: consent?.analytics ?? false,
    });

    const toggle = (key: keyof typeof prefs) =>
        setPrefs((prev) => ({ ...prev, [key]: !prev[key] }));

    return (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-overlay/50 backdrop-blur-sm">
            <div className="mx-4 w-full max-w-md rounded-xl border border-divider bg-content1 p-6 shadow-2xl">
                <h2 className="text-base font-semibold text-foreground">Cookie Preferences</h2>
                <p className="mt-1 text-xs text-foreground-500">
                    Choose which cookies you&apos;d like to allow. Necessary cookies are always
                    enabled and cannot be turned off.
                </p>

                <div className="mt-5 space-y-4">
                    {/* Necessary — always on */}
                    <div className="flex items-start justify-between gap-3">
                        <div>
                            <p className="text-sm font-medium text-foreground">
                                Necessary Cookies
                            </p>
                            <p className="text-xs text-foreground-500">
                                Required for the platform to function. These include
                                authentication and session cookies.
                            </p>
                        </div>
                        <div className="relative inline-flex h-6 w-10 shrink-0 cursor-not-allowed items-center rounded-full bg-primary opacity-60">
                            <span className="inline-block h-4 w-4 translate-x-5 rounded-full bg-content1 transition" />
                        </div>
                    </div>

                    {categories.map(({ key, label, description }) => (
                        <div key={key} className="flex items-start justify-between gap-3">
                            <div>
                                <p className="text-sm font-medium text-foreground">{label}</p>
                                <p className="text-xs text-foreground-500">
                                    {description}
                                </p>
                            </div>
                            <button
                                type="button"
                                role="switch"
                                aria-checked={prefs[key]}
                                onClick={() => toggle(key)}
                                className={`relative inline-flex h-6 w-10 shrink-0 cursor-pointer items-center rounded-full transition-colors ${
                                    prefs[key]
                                        ? 'bg-primary'
                                        : 'bg-default-300'
                                }`}
                            >
                                <span
                                    className={`inline-block h-4 w-4 rounded-full bg-content1 transition-transform ${
                                        prefs[key] ? 'translate-x-5' : 'translate-x-1'
                                    }`}
                                />
                            </button>
                        </div>
                    ))}
                </div>

                <div className="mt-6 flex justify-end gap-2">
                    <button
                        onClick={closePreferences}
                        className="rounded-lg border border-default-300 px-4 py-2 text-xs font-medium text-foreground-600 transition-colors hover:bg-content2"
                    >
                        Cancel
                    </button>
                    <button
                        onClick={acceptAll}
                        className="rounded-lg border border-default-300 px-4 py-2 text-xs font-medium text-foreground-600 transition-colors hover:bg-content2"
                    >
                        Accept All
                    </button>
                    <button
                        onClick={() => savePreferences(prefs)}
                        className="rounded-lg bg-primary px-4 py-2 text-xs font-medium text-primary-foreground transition-colors hover:bg-primary-600"
                    >
                        Save Preferences
                    </button>
                </div>
            </div>
        </div>
    );
}

