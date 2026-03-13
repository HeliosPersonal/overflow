'use client';

import {HomeIcon, TagIcon, TrophyIcon, HandRaisedIcon} from "@heroicons/react/24/outline";
import {Listbox, ListboxItem} from "@heroui/listbox";
import {usePathname} from "next/navigation";
import CookieSettingsButton from "@/components/cookie/CookieSettingsButton";

export default function SideMenu({ isAdmin = false }: { isAdmin?: boolean }) {
    const pathname = usePathname();

    // ...existing code...

    const navLinks = [
        {key: 'poker', icon: HandRaisedIcon, text: 'Planning Poker', href: '/planning-poker'},
        {key: 'home', icon: HomeIcon, text: 'Questions', href: '/questions'},
        ...(isAdmin ? [{key: 'tags', icon: TagIcon, text: 'Tags', href: '/tags'}] : []),
        {key: 'leaderboard', icon: TrophyIcon, text: 'Leaderboard', href: '/profiles'},
    ]
    
    return (
        <div className="flex flex-col h-full">
            <Listbox
                aria-label='nav links'
                variant='faded'
                items={navLinks}
                className='ml-6'
            >
                {({key, href, icon: Icon, text}) => (
                    <ListboxItem
                        href={href}
                        aria-labelledby={key}
                        aria-describedby={text}
                        key={key}
                        startContent={<Icon className='h-6' />}
                        classNames={{
                            base: pathname.startsWith(href) ? 'text-primary' : '',
                            title: 'text-lg'
                        }}
                    >
                        {text}
                    </ListboxItem>
                )}
            </Listbox>
            <div className="mt-auto ml-6 pb-4">
                <CookieSettingsButton />
            </div>
        </div>
    );
}