'use client';

import {Button} from "@heroui/button";
import {useTheme} from "next-themes";
import {MoonIcon, SunIcon} from "@heroicons/react/24/outline";
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
            color='primary'
            variant='light'
            isIconOnly
            aria-label='Toggle Theme'
            onPress={() => setTheme(theme === 'light' ? 'dark' : 'light')}
        >
            {theme === 'light' ? (
                <MoonIcon className='h-8' />
            ) : (
                <SunIcon className='h-8 text-yellow-300' />
            )}
        </Button>
    );
}