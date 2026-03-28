'use client';

import {Select, SelectItem} from "@heroui/select";
import {usePathname, useRouter, useSearchParams} from "next/navigation";
import {Key, useMemo} from "react";

type Props = {
    answerCount: number;
}

export default function AnswersHeader({answerCount}: Props) {
    const router = useRouter();
    const pathname = usePathname();
    const searchParams = useSearchParams();
    
    const current = searchParams.get('sort') === 'created' ? 'created' : 'highScore';
    const selectedKeys = useMemo(() => new Set([current]), [current]);
    
    const handleSort = (key: Key) => {
        const params = new URLSearchParams(searchParams);
        if (key === 'highScore') {
            params.delete('sort');
        } else {
            params.set('sort', key.toString());
        }
        router.replace(`${pathname}?${params}`, {scroll: false});
    }
    
    return (
        <div className='flex items-center justify-between pt-3 w-full px-3 sm:px-6'>
            <h2 className='text-base sm:text-xl'>{answerCount} {answerCount === 1 ? 'Answer' : 'Answers'}</h2>
            <div className='flex items-center gap-3 justify-end w-auto max-w-[200px] sm:max-w-[240px] ml-auto'>
                <Select
                    aria-label='Select sorting'
                    selectedKeys={selectedKeys}
                    onSelectionChange={(keys) => {
                        const [key] = Array.from(keys as Set<string>);
                        handleSort(key);
                    }}
                >
                    <SelectItem key='highScore'>Highest score (default)</SelectItem>
                    <SelectItem key='created'>Date created</SelectItem>
                </Select>
            </div>
        </div>
    );
}