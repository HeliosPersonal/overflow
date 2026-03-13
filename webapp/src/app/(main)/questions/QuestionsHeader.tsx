'use client';

import {Button} from "@heroui/button";
import Link from "next/link";
import {Tab, Tabs} from "@heroui/tabs";
import {useTagStore} from "@/lib/hooks/useTagStore";
import {useRouter, useSearchParams} from "next/navigation";
import {Key} from "react";
import SearchInput from "@/components/nav/SearchInput";

type Props = {
    tag?: string;
    total: number;
}

export default function QuestionsHeader({tag, total}: Props) {
    const router = useRouter();
    const searchParams = useSearchParams();
    const selectedTag = useTagStore(state => state.getTagBySlug(tag ?? ''))
    const tabs = [
        {key: 'newest', label: 'Newest'},
        {key: 'active', label: 'Active'},
        {key: 'unanswered', label: 'Unanswered'},
    ]

    const selected = searchParams.get('sort') ?? 'newest';

    const handleTabChange = (tab: Key) => {
        const params = new URLSearchParams(searchParams);
        params.set('sort', tab.toString());
        router.push(`?${params.toString()}`);
    }

    return (
        <div className='flex flex-col w-full border-b border-content2 gap-4 pb-4 pt-4'>
            <div className='flex justify-between px-6'>
                <div className='flex flex-col items-start gap-2'>
                    <h1>
                        {tag ? `[${tag}]` : 'Newest Questions'}
                    </h1>
                    <p className='font-light'>{selectedTag?.description}</p>
                </div>
                <Link href='/questions/ask'>
                    <Button color='primary'>
                        Ask Question
                    </Button>
                </Link>
            </div>
            <div className='px-6'>
                <SearchInput />
            </div>
            <div className='flex justify-between px-6 items-center'>
                <div>{total} {total === 1 ? 'Question' : 'Questions'}</div>
                <div className='flex items-center gap-4'>
                    <Tabs
                        selectedKey={selected}
                        onSelectionChange={handleTabChange}
                    >
                        {tabs.map(item => (
                            <Tab key={item.key} title={item.label}/>
                        ))}
                    </Tabs>
                </div>
            </div>
        </div>
    );
}