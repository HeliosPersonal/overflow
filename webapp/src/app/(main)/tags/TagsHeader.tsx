'use client'

import {Search} from '@/components/animated-icons'
import {Input} from "@heroui/input";
import {Tab, Tabs} from "@heroui/tabs";
import {useRouter} from "next/navigation";
import {Button} from "@heroui/button";
import Link from "next/link";

type Props = {
    isAdmin?: boolean;
}

export default function TagHeader({isAdmin}: Props) {
    const router = useRouter();
    const tabs = [
        {key: 'popular', label: 'Popular'},
        {key: 'name', label: 'Name'}
    ]

    return (
        <div className='flex flex-col w-full gap-4 pb-4 pt-4'>
            <div className='flex items-start justify-between'>
                <div className='flex flex-col items-start gap-3'>
                    <h1>Tags</h1>
                    <p className='text-sm text-foreground-500'>A tag is a keyword or label that categorizes your question with other,
                        similar questions. Using the right tags makes it easier for others to find
                        and answer your question.</p>
                </div>
                {isAdmin && (
                    <Button as={Link} href='/tags/manage' color='primary'>
                        Edit tags
                    </Button>
                )}
            </div>
            <div className='flex items-center justify-between'>
                <Input
                    type="search"
                    className='w-fit'
                    required
                    placeholder="Search"
                    startContent={<Search size={24} className='text-foreground-400'/>}
                />

                <Tabs
                    onSelectionChange={(key) => router.push(`/tags?sort=${key}`)}
                    defaultSelectedKey='name'
                >
                    {tabs.map((item) => (
                        <Tab key={item.key} title={item.label}/>
                    ))}
                </Tabs>

            </div>
        </div>
    )
}