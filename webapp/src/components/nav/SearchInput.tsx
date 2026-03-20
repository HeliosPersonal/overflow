'use client';

import {Input} from "@heroui/input";
import {Search} from "@/components/animated-icons/Search";
import {useEffect, useRef, useState} from "react";
import {Question} from "@/lib/types";
import {searchQuestions} from "@/lib/actions/question-actions";
import {Listbox, ListboxItem} from "@heroui/listbox";
import {Spinner} from "@heroui/spinner";

const DEBOUNCE_MS = 300;
const BLUR_DELAY_MS = 150;

export default function SearchInput() {
    const [query, setQuery] = useState('');
    const [loading, setLoading] = useState(false);
    const [results, setResults] = useState<Question[] | null>(null);
    const [showDropdown, setShowDropdown] = useState(false);
    const containerRef = useRef<HTMLDivElement | null>(null);

    useEffect(() => {
        if (!query) {
            setResults(null);
            setShowDropdown(false);
            return;
        }

        const timer = setTimeout(async () => {
            setLoading(true);
            const {data: questions} = await searchQuestions(query);
            setResults(questions);
            setLoading(false);
            setShowDropdown(true);
        }, DEBOUNCE_MS);

        return () => clearTimeout(timer);
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
        }, BLUR_DELAY_MS);
    };

    const handleFocus = () => {
        if (results && results.length > 0) setShowDropdown(true);
    };

    return (
        <div ref={containerRef} className='relative flex flex-col w-full' onBlur={handleBlur}>
            <Input
                startContent={<Search size={24} />}
                type='search'
                placeholder='Search questions...'
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onFocus={handleFocus}
                endContent={loading && <Spinner size='sm' />}
            />
            {showDropdown && results && (
                <div
                    className='absolute top-full z-50 bg-content1 shadow-lg border-2 border-content3 w-full'>
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
                                        border border-content3 rounded-md'>
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