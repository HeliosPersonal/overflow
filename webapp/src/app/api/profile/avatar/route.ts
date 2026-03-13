import { NextRequest, NextResponse } from 'next/server';
import { auth } from '@/auth';

/**
 * PUT /api/profile/avatar
 *
 * Proxies the avatar update to ProfileService's PUT /profiles/edit endpoint.
 * Used after guest sign-in to persist the avatar chosen during account creation.
 *
 * Body: { avatarUrl: string }
 */
export async function PUT(request: NextRequest) {
    const session = await auth();
    if (!session?.accessToken) {
        return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    const { avatarUrl } = await request.json();
    if (!avatarUrl || typeof avatarUrl !== 'string') {
        return NextResponse.json({ error: 'avatarUrl is required' }, { status: 400 });
    }

    const apiUrl = process.env.API_URL;
    if (!apiUrl) {
        return NextResponse.json({ error: 'API_URL not configured' }, { status: 500 });
    }

    const res = await fetch(`${apiUrl}/profiles/edit`, {
        method: 'PUT',
        headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer ${session.accessToken}`,
        },
        body: JSON.stringify({ avatarUrl }),
    });

    if (!res.ok) {
        const body = await res.text();
        console.error('[AvatarUpdate] Failed to update avatar:', res.status, body);
        return NextResponse.json({ error: 'Failed to update avatar' }, { status: res.status });
    }

    return NextResponse.json({ ok: true });
}

