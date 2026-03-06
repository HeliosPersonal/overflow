'use client';

import {useState} from "react";
import {useRouter, useSearchParams} from "next/navigation";
import {Button} from "@heroui/button";
import {Pagination} from "@heroui/pagination";

type Props = {
    totalCount: number;
}

const PAGE_SIZES = [2, 5, 10, 20]

export default function AppPagination({totalCount}: Props) {
    const router = useRouter();
    const searchParams = useSearchParams();
    
    // Initialize from URL params
    const [currentPage, setCurrentPage] = useState(() => {
        return Number(searchParams.get('page')) || 1;
    });
    const [pageSize, setPageSize] = useState(() => {
        return Number(searchParams.get('pageSize')) || 5;
    });

    const updateURL = (page: number, size: number) => {
        const params = new URLSearchParams(searchParams);
        params.set('page', page.toString());
        params.set('pageSize', size.toString());
        router.push(`?${params.toString()}`, {scroll: false});
    };

    return (
        <div className='flex justify-between items-center pt-3 pb-6 px-6'>
            <div className='flex items-center gap-2'>
                <span>Page size: </span>
                <div className='flex items-center gap-1'>
                    {PAGE_SIZES.map((size, i) => (
                        <Button
                            key={i}
                            type='button'
                            variant={size === pageSize ? 'solid' : 'bordered'}
                            isIconOnly
                            size='sm'
                            onPress={() => {
                                setCurrentPage(1);
                                setPageSize(size);
                                updateURL(1, size);
                            }}
                        >
                            {size}
                        </Button>
                    ))}
                </div>
            </div>
            <div className='flex items-center gap-3'>
                <span className='text-sm text-default-500'>
                    Page {currentPage} of {Math.ceil(totalCount / pageSize)}
                </span>
                <Pagination
                    total={Math.ceil(totalCount / pageSize)}
                    onChange={(page) => {
                        setCurrentPage(page);
                        updateURL(page, pageSize);
                    }}
                    page={currentPage}
                    className='cursor-pointer'
                    classNames={{
                        cursor: 'bg-default-200 text-default-800 shadow-none',
                    }}
                />
            </div>
        </div>
    );
}