import { NextRequest, NextResponse } from 'next/server';
import { authConfig } from '@/lib/config';

export async function POST(request: NextRequest) {
    try {
        const { email } = await request.json();

        if (!email) {
            return NextResponse.json(
                { error: 'Email is required' },
                { status: 400 }
            );
        }

        // Get admin access token
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
        const { baseUrl, realmName } = extractKeycloakConfig();

        // Get user by email
        const usersUrl = `${baseUrl}/admin/realms/${realmName}/users?email=${encodeURIComponent(email)}&exact=true`;
        
        const usersResponse = await fetch(usersUrl, {
            headers: {
                'Authorization': `Bearer ${adminToken.access_token}`,
            },
        });

        if (!usersResponse.ok) {
            console.error('Failed to search for user');
            // Return success anyway to avoid email enumeration
            return NextResponse.json({ message: 'If an account exists, a password reset email has been sent.' });
        }

        const users = await usersResponse.json();

        // If user exists, send password reset email via Keycloak
        if (users && users.length > 0) {
            const userId = users[0].id;

            // Execute password reset action - Keycloak sends the email
            const resetUrl = `${baseUrl}/admin/realms/${realmName}/users/${userId}/execute-actions-email`;
            const resetResponse = await fetch(resetUrl, {
                method: 'PUT',
                headers: {
                    'Authorization': `Bearer ${adminToken.access_token}`,
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(['UPDATE_PASSWORD']),
            });

            if (!resetResponse.ok) {
                const errorText = await resetResponse.text();
                console.error('Failed to send reset email:', errorText);
            }
        }

        // Always return success to prevent email enumeration
        return NextResponse.json({ 
            message: 'If an account exists, a password reset email has been sent.' 
        });

    } catch (error) {
        console.error('Forgot password API error:', error);
        return NextResponse.json(
            { error: 'An unexpected error occurred. Please try again.' },
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

