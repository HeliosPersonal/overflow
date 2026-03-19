import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type CookieCategory = 'necessary' | 'functional' | 'analytics';

export interface CookieConsent {
    necessary: boolean;   // Always true — cannot be disabled
    functional: boolean;  // Theme preference, tag cache, etc.
    analytics: boolean;   // OpenTelemetry / future analytics
}

interface CookieConsentState {
    consent: CookieConsent | null;       // null = not yet decided
    showBanner: boolean;
    showPreferences: boolean;
    acceptAll: () => void;
    rejectNonEssential: () => void;
    savePreferences: (prefs: Omit<CookieConsent, 'necessary'>) => void;
    openPreferences: () => void;
    closePreferences: () => void;
    resetConsent: () => void;
    hasConsented: () => boolean;
}

export const useCookieConsentStore = create<CookieConsentState>()(
    persist(
        (set, get) => ({
            consent: null,
            showBanner: true,
            showPreferences: false,

            acceptAll: () =>
                set({
                    consent: { necessary: true, functional: true, analytics: true },
                    showBanner: false,
                    showPreferences: false,
                }),

            rejectNonEssential: () =>
                set({
                    consent: { necessary: true, functional: false, analytics: false },
                    showBanner: false,
                    showPreferences: false,
                }),

            savePreferences: (prefs) =>
                set({
                    consent: { necessary: true, ...prefs },
                    showBanner: false,
                    showPreferences: false,
                }),

            openPreferences: () => set({ showPreferences: true }),
            closePreferences: () => set({ showPreferences: false }),

            resetConsent: () =>
                set({ consent: null, showBanner: true, showPreferences: false }),

            hasConsented: () => get().consent !== null,
        }),
        {
            name: 'overflow-cookie-consent',
            // Only persist `consent` — UI flags reset on load
            partialize: (state) => ({ consent: state.consent }),
            onRehydrateStorage: () => {
                return (state) => {
                    if (state) {
                        state.showBanner = state.consent === null;
                    }
                };
            },
        }
    )
);

