'use client';

import {
    getKeyValue,
    Table,
    TableBody,
    TableCell,
    TableColumn,
    TableHeader,
    TableRow
} from "@heroui/table";
import {Profile} from "@/lib/types";
import {useRouter} from "next/navigation";
import {SortDescriptor} from "@heroui/react";

type Props = {
    profiles: Profile[];
}

export default function ProfilesList({profiles}: Props) {
    const router = useRouter();
    const columns = [
        {key: 'displayName', label: 'Display Name'},
        {key: 'reputation', label: 'Reputation'},
    ]

    const onSortChange = (sort: SortDescriptor) => {
        router.push(`/profiles?sortBy=${sort.column}`);
    }

    return (
        <div className="rounded-xl overflow-hidden border border-content3 shadow-inset-md bg-content2">
        <Table
            onSortChange={(sort) => onSortChange(sort)}
            sortDescriptor={{column: 'reputation', direction: 'descending'}}
            aria-label='User profiles'
            selectionMode='single'
            radius='none'
            onRowAction={(key) => router.push(`/profiles/${String(key)}`)}
            classNames={{
                th: 'bg-content3 text-foreground-500 uppercase text-xs tracking-wide font-semibold',
                td: 'text-foreground-600',
                tr: 'hover:bg-content3 transition-colors duration-150 cursor-pointer',
            }}
            removeWrapper
        >
            <TableHeader columns={columns}>
                {(column) =>
                    <TableColumn key={column.key} allowsSorting>
                        {column.label}
                    </TableColumn>}
            </TableHeader>
            <TableBody items={profiles}>
                {(item) => (
                    <TableRow key={item.userId}>
                        {(columnKey) => <TableCell>{getKeyValue(item, columnKey)}</TableCell>}
                    </TableRow>
                )}
            </TableBody>
        </Table>
        </div>
    );
}