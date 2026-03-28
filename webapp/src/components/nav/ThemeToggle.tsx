'use client';

import {Button} from "@heroui/button";
import {useTheme} from "next-themes";
import {Sun} from "@/components/animated-icons/Sun";
import {Moon} from "@/components/animated-icons/Moon";
import {useEffect, useState} from "react";
import {updateThemePreference} from "@/lib/actions/profile-actions";
import {ThemePreference} from "@/lib/types";

export default function ThemeToggle() {
    const {theme, setTheme} = useTheme();
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        setMounted(true);
    }, []);

    if (!mounted) return null;

    function handleToggle() {
        const next = theme === 'light' ? 'dark' : 'light';
        setTheme(next);
        // Persist to profile (fire-and-forget — fails silently for logged-out users)
        const preference: ThemePreference = next === 'dark' ? 'Dark' : 'Light';
        void updateThemePreference(preference);
    }

    return (
        <Button
            color='default'
            variant='light'
            isIconOnly
            aria-label='Toggle Theme'
            onPress={handleToggle}
        >
            {theme === 'light' ? (
                <Moon size={24} />
            ) : (
                <Sun size={28} />
            )}
        </Button>
    );
}