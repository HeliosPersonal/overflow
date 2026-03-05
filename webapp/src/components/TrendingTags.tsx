'use client';

import {getTrendingTags} from "@/lib/actions/tag-actions";
import {Progress} from "@heroui/react";
import Link from "next/link";
import {useEffect, useState} from "react";
import {TrendingTag} from "@/lib/types";

export default function TrendingTags() {
    const [tags, setTags] = useState<TrendingTag[] | null>(null);
    const [error, setError] = useState<boolean>(false);

    useEffect(() => {
        getTrendingTags().then(result => {
            if (result.error) {
                setError(true);
            } else {
                setTags(result.data);
            }
        });
    }, []);

    // Calculate max count for progress percentage
    const maxCount = Array.isArray(tags) && tags.length > 0
        ? Math.max(...tags.map(tag => tag.count))
        : 1;

    return (
        <div className='bg-primary-50 p-6 rounded-2xl'>
            <h3 className='text-2xl text-primary mb-5 text-center'>Trending tags this week</h3>
            <div className='flex flex-col px-6 gap-4'>
                {error ? (
                    <div>Unavailable</div>
                ) : (
                    <>
                        {Array.isArray(tags) && tags.map(tag => {
                            const percentage = (tag.count / maxCount) * 100;
                            return (
                                <div key={tag.tag} className='flex flex-col gap-1'>
                                    <Link href={`/?tag=${tag.tag}`} className='text-sm font-medium hover:underline'>
                                        {tag.tag}
                                    </Link>
                                    <Progress
                                        aria-label={`${tag.tag} usage`}
                                        color="primary"
                                        showValueLabel={true}
                                        size="md"
                                        value={percentage}
                                        formatOptions={{style: "decimal"}}
                                        valueLabel={`${tag.count} uses`}
                                    />
                                </div>
                            );
                        })}
                    </>
                )}
            </div>
        </div>
    );
}