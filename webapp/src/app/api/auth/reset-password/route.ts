import { NextRequest, NextResponse } from 'next/server';
import { authConfig } from '@/lib/config';
import { verifyResetToken, consumeResetToken } from '@/lib/resetTokens';

export async function POST(request: NextRequest) {
    try {
        const { token, email, password } = await request.json();

        if (!token || !email || !password) {
            return NextResponse.json(
                { error: 'Missing required fields' },
                { status: 400 }
            );
        }

        // Verify reset token
        const tokenData = verifyResetToken(token);
        
        if (!tokenData.valid || tokenData.email !== email) {
            return NextResponse.json(
                { error: 'Invalid or expired reset token' },
                { status: 400 }
            );
        }

        // Get admin token
        const adminTokenResponse = await fetch(
            `${authConfig.kcInternal}/protocol/openid-connect/token`,
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    grant_type: 'client_credentials',
                    client_id: authConfig.kcAdminClientId,
                    client_secret: authConfig.kcAdminClientSecret,
                }),
            }
        );

        if (!adminTokenResponse.ok) {
            console.error('Failed to get admin token');
            return NextResponse.json(
                { error: 'Service temporarily unavailable' },
                { status: 503 }
            );
        }

        const adminToken = await adminTokenResponse.json();
        const { baseUrl, realmName} = extractKeycloakConfig();

        // Find user by email
        const usersUrl = `${baseUrl}/admin/realms/${realmName}/users?email=${encodeURIComponent(email)}&exact=true`;
        const usersResponse = await fetch(usersUrl, {
            headers: {
                'Authorization': `Bearer ${adminToken.access_token}`,
            },
        });

        if (!usersResponse.ok) {
            console.error('Failed to find user');
            return NextResponse.json(
                { error: 'User not found' },
                { status: 404 }
            );
        }

        const users = await usersResponse.json();

        if (!users || users.length === 0) {
            return NextResponse.json(
                { error: 'User not found' },
                { status: 404 }
            );
        }

        const userId = users[0].id;

        // Reset password via Keycloak Admin API
        const resetUrl = `${baseUrl}/admin/realms/${realmName}/users/${userId}/reset-password`;
        const resetResponse = await fetch(resetUrl, {
            method: 'PUT',
            headers: {
                'Authorization': `Bearer ${adminToken.access_token}`,
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                type: 'password',
                value: password,
                temporary: false,
            }),
        });

        if (!resetResponse.ok) {
            const errorText = await resetResponse.text();
            console.error('Failed to reset password:', errorText);
            return NextResponse.json(
                { error: 'Failed to reset password' },
                { status: resetResponse.status }
            );
        }

        // Consume the token so it can't be reused
        consumeResetToken(token);

        return NextResponse.json({
            message: 'Password reset successfully',
        });

    } catch (error) {
        console.error('Reset password API error:', error);
        return NextResponse.json(
            { error: 'An unexpected error occurred' },
            { status: 500 }
        );
    }
}

function extractKeycloakConfig(): { baseUrl: string; realmName: string } {
    const issuer = authConfig.kcInternal;
    const parts = issuer.split('/realms/');
    const baseUrl = parts[0];
    const realmName = parts[1] || 'overflow-staging';
    return { baseUrl, realmName };
}

