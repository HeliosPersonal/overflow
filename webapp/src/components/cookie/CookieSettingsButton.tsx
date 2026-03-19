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
                className="text-xs text-foreground-400 hover:text-foreground-600 transition-colors"
            >
                🍪 Cookie Settings
            </button>
            {showPreferences && <CookiePreferencesModal />}
        </>
    );
}

