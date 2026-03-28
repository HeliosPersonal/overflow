'use client';

import { useCookieConsentStore } from '@/lib/hooks/useCookieConsentStore';
import CookiePreferencesModal from './CookiePreferencesModal';

/**
 * A small footer-style link to re-open cookie preferences.
 * Render this at the bottom of a sidebar or footer.
 */
export default function CookieSettingsButton() {
    const { openPreferences, showPreferences } = useCookieConsentStore();

    return (
        <>
            <button
                onClick={openPreferences}
                className="flex items-center justify-center
                    w-8 h-8 rounded-full bg-content2 border border-content3 shadow-sm text-sm
                    sm:w-auto sm:h-auto sm:rounded-lg sm:bg-transparent sm:border-0 sm:shadow-none sm:text-xs
                    text-foreground-400 hover:text-foreground-600 transition-colors"
                aria-label="Cookie Settings"
            >
                <span className="sm:hidden">🍪</span>
                <span className="hidden sm:inline">🍪 Cookie Settings</span>
            </button>
            {showPreferences && <CookiePreferencesModal />}
        </>
    );
}

