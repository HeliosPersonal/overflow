'use client';

import {HomeIcon, TagIcon, UserIcon, UserGroupIcon} from "@heroicons/react/24/outline";
import {Listbox, ListboxItem} from "@heroui/listbox";
import {usePathname} from "next/navigation";

export default function SideMenu() {
    const pathname = usePathname();
    const navLinks = [
        {key: 'home', icon: HomeIcon, text: 'Questions', href: '/'},
        {key: 'tags', icon: TagIcon, text: 'Tags', href: '/tags'},
        {key: 'session', icon: UserIcon, text: 'User Session', href: '/session'},
        {key: 'profiles', icon: UserGroupIcon, text: 'Profiles', href: '/profiles'},
    ]
    
    return (
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
                        base: pathname === href ? 'text-primary' : '',
                        title: 'text-lg'
                    }}
                >
                    {text}
                </ListboxItem>
            )}
        </Listbox>
    );
}