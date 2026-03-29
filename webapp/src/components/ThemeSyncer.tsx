'use client';

import {useTheme} from "next-themes";
import {useEffect, useRef} from "react";
import {getMyThemePreference} from "@/lib/actions/profile-actions";

/**
 * Fetches the user's stored theme preference from ProfileService on mount
 * and applies it via next-themes. Runs once — subsequent toggles are handled
 * by ThemeToggle (which persists to profile AND updates next-themes locally).
 *
 * For unauthenticated users the server action returns null and we skip,
 * so next-themes falls back to localStorage / system default as usual.
 */
export default function ThemeSyncer() {
    const {setTheme} = useTheme();
    const fetched = useRef(false);

    useEffect(() => {
        if (fetched.current) return;
        fetched.current = true;

        getMyThemePreference().then((pref) => {
            if (pref) setTheme(pref.toLowerCase());
        });
    }, [setTheme]);

    return null;
}

