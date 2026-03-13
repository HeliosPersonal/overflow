'use client';

import {Input} from "@heroui/input";
import {MagnifyingGlassIcon} from "@heroicons/react/24/outline";
import {useEffect, useRef, useState} from "react";
import {Question} from "@/lib/types";
import {searchQuestions} from "@/lib/actions/question-actions";
import {Listbox, ListboxItem} from "@heroui/listbox";
import {Spinner} from "@heroui/spinner";

export default function SearchInput() {
    const [query, setQuery] = useState('');
    const [loading, setLoading] = useState(false);
    const [results, setResults] = useState<Question[] | null>(null);
    const [showDropdown, setShowDropdown] = useState(false);
    const timeoutRef = useRef<NodeJS.Timeout | null>(null);
    const containerRef = useRef<HTMLDivElement | null>(null);

    useEffect(() => {
        if (timeoutRef.current) clearTimeout(timeoutRef.current);

        if (!query) {
            timeoutRef.current = setTimeout(() => {
                setResults(null);
                setShowDropdown(false);
            }, 0);
            return;
        }

        timeoutRef.current = setTimeout(async () => {
            setLoading(true);
            const {data: questions} = await searchQuestions(query);
            setResults(questions);
            setLoading(false);
            setShowDropdown(true);
        }, 300);
    }, [query]);

    const onAction = () => {
        setQuery('');
        setResults(null);
        setShowDropdown(false);
    };

    const handleBlur = () => {
        setTimeout(() => {
            if (containerRef.current && !containerRef.current.contains(document.activeElement)) {
                setShowDropdown(false);
            }
        }, 150);
    };

    const handleFocus = () => {
        if (results && results.length > 0) setShowDropdown(true);
    };

    return (
        <div ref={containerRef} className='relative flex flex-col w-full' onBlur={handleBlur}>
            <Input
                startContent={<MagnifyingGlassIcon className='size-6' />}
                type='search'
                placeholder='Search questions...'
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onFocus={handleFocus}
                endContent={loading && <Spinner size='sm' />}
            />
            {showDropdown && results && (
                <div
                    className='absolute top-full z-50 bg-white dark:bg-default-50 shadow-lg border-2 border-neutral-800 w-full'>
                    <Listbox
                        onAction={onAction}
                        items={results}
                        className='flex flex-col overflow-y-auto'
                    >
                        {question => (
                            <ListboxItem
                                href={`/questions/${question.id}`}
                                key={question.id}
                                startContent={
                                    <div className='flex flex-col h-14 min-w-14 justify-center items-center
                                        border border-neutral-200 dark:border-neutral-800 rounded-md'>
                                        <span>{question.answerCount}</span>
                                        <span className='text-xs'>answers</span>
                                    </div>
                                }
                            >
                                <div>
                                    <div className='font-semibold'>{question.title}</div>
                                    <div className='text-xs opacity-60 line-clamp-2'>{question.content}</div>
                                </div>
                            </ListboxItem>
                        )}
                    </Listbox>
                </div>
            )}
        </div>

    );
}