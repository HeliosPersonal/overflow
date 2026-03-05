'use client';

import {getTopUsers} from "@/lib/actions/profile-actions";
import {Progress} from "@heroui/react";
import {useEffect, useState} from "react";
import {TopUserWithProfile} from "@/lib/types";

export default function TopUsers() {
    const [users, setUsers] = useState<TopUserWithProfile[] | null>(null);
    const [error, setError] = useState<boolean>(false);

    useEffect(() => {
        getTopUsers().then(result => {
            if (result.error) {
                setError(true);
            } else {
                setUsers(result.data);
            }
        });
    }, []);

    const filteredUsers = Array.isArray(users) ? users.filter(u => u.profile) : [];

    // Calculate max delta for progress percentage
    const maxDelta = filteredUsers.length > 0
        ? Math.max(...filteredUsers.map(u => u.delta))
        : 1;

    return (
        <div className='bg-default-100 border border-default-200 p-6 rounded-2xl'>
            <h3 className='text-lg font-semibold text-foreground-600 mb-5'>Most points this week</h3>
            <div className='flex flex-col px-6 gap-4'>
                {error ? (
                    <div>Unavailable</div>
                ) : (
                    <>
                        {filteredUsers.map(u => {
                            const percentage = (u.delta / maxDelta) * 100;
                            return (
                                <div key={u.userId} className='flex flex-col gap-1'>
                                    <div className='text-sm font-medium'>
                                        {u.profile!.displayName}
                                    </div>
                                    <Progress
                                        aria-label={`${u.profile!.displayName} points`}
                                        color="primary"
                                        showValueLabel={true}
                                        size="md"
                                        value={percentage}
                                        formatOptions={{style: "decimal"}}
                                        valueLabel={`${u.delta} points`}
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