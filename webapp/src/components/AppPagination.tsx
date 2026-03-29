'use client';

import {useRouter, useSearchParams} from "next/navigation";
import {Button} from "@heroui/button";
import {Pagination} from "@heroui/pagination";

type Props = {
    totalCount: number;
}

const PAGE_SIZES = [5, 10, 20];
const DEFAULT_PAGE_SIZE = 5;

export default function AppPagination({totalCount}: Props) {
    const router = useRouter();
    const searchParams = useSearchParams();

    const currentPage = Number(searchParams.get('page')) || 1;
    const pageSize = Number(searchParams.get('pageSize')) || DEFAULT_PAGE_SIZE;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

    const updateURL = (page: number, size: number) => {
        const params = new URLSearchParams(searchParams);
        params.set('page', page.toString());
        params.set('pageSize', size.toString());
        router.push(`?${params.toString()}`, {scroll: false});
    };

    return (
        <div className='bg-content1 border-t border-content2 flex flex-col sm:flex-row sm:justify-between items-center gap-3 pt-3 pb-4 px-4 sm:px-6'>
            <div className='flex items-center gap-2'>
                <span className='text-xs sm:text-sm text-foreground-500'>Page size: </span>
                <div className='flex items-center gap-1'>
                    {PAGE_SIZES.map(size => (
                        <Button
                            key={size}
                            type='button'
                            variant={size === pageSize ? 'flat' : 'light'}
                            className={size === pageSize ? 'bg-content3 text-foreground-800' : 'text-foreground-500'}
                            isIconOnly
                            size='sm'
                            onPress={() => updateURL(1, size)}
                        >
                            {size}
                        </Button>
                    ))}
                </div>
            </div>
            <div className='flex items-center gap-2 sm:gap-3'>
                <span className='text-xs sm:text-sm text-default-500'>
                    Page {currentPage} of {totalPages}
                </span>
                <Pagination
                    total={totalPages}
                    onChange={(page) => updateURL(page, pageSize)}
                    page={currentPage}
                    size='sm'
                    className='cursor-pointer'
                    classNames={{
                        cursor: 'bg-default-200 text-default-800 shadow-none',
                    }}
                />
            </div>
        </div>
    );
}