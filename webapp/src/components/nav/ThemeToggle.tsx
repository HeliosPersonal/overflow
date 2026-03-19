'use client';

import {Button} from "@heroui/button";
import {useTheme} from "next-themes";
import {Sun} from "@/components/animated-icons/Sun";
import {Moon} from "@/components/animated-icons/Moon";
import {useEffect, useState} from "react";

export default function ThemeToggle() {
    const {theme, setTheme} = useTheme();
    const [mounted, setMounted] = useState(false);

    useEffect(() => {
        const mount = () => setMounted(true);
        mount();
    }, []);

    if (!mounted) return null;

    return (
        <Button
            color='default'
            variant='light'
            isIconOnly
            aria-label='Toggle Theme'
            onPress={() => setTheme(theme === 'light' ? 'dark' : 'light')}
        >
            {theme === 'light' ? (
                <Moon size={24} />
            ) : (
                <Sun size={28} />
            )}
        </Button>
    );
}