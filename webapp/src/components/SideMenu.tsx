'use client';

import {HomeIcon, TagIcon, TrophyIcon, Squares2X2Icon} from "@heroicons/react/24/outline";
import {Listbox, ListboxItem} from "@heroui/listbox";
import {usePathname} from "next/navigation";

export default function SideMenu({ isAdmin = false }: { isAdmin?: boolean }) {
    const pathname = usePathname();

    const navLinks = [
        {key: 'poker', icon: Squares2X2Icon, text: 'Dashboard', href: '/planning-poker'},
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
                            base: pathname.startsWith(href)
                                ? 'bg-content3 shadow-raise-sm rounded-xl text-foreground-800'
                                : 'hover:bg-content2 rounded-xl text-foreground-500',
                            title: 'text-base font-medium'
                        }}
                    >
                        {text}
                    </ListboxItem>
                )}
            </Listbox>
        </div>
    );
}