import { NextRequest, NextResponse } from 'next/server';
import { authConfig } from '@/lib/config';
import logger from '@/lib/logger';

export async function POST(request: NextRequest) {
    try {
        const { email, password } = await request.json();

        if (!email || !password) {
            return NextResponse.json(
                { error: 'Email and password are required' },
                { status: 400 }
            );
        }

        // Exchange credentials for tokens using Keycloak's Direct Access Grant (Resource Owner Password Credentials)
        const tokenResponse = await fetch(
            `${authConfig.kcInternal}/protocol/openid-connect/token`,
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: new URLSearchParams({
                    grant_type: 'password',
                    client_id: authConfig.kcClientId,
                    client_secret: authConfig.kcSecret,
                    username: email,
                    password: password,
                    scope: 'openid profile email offline_access',
                }),
            }
        );

        const tokenData = await tokenResponse.json();

        if (!tokenResponse.ok) {
            logger.error({ body: tokenData }, 'Keycloak login error');
            
            // Handle specific Keycloak errors
            if (tokenData.error === 'invalid_grant') {
                return NextResponse.json(
                    { error: 'Invalid email or password' },
                    { status: 401 }
                );
            }
            
            return NextResponse.json(
                { error: tokenData.error_description || 'Authentication failed' },
                { status: tokenResponse.status }
            );
        }

        // Return the tokens to the client
        return NextResponse.json({
            access_token: tokenData.access_token,
            refresh_token: tokenData.refresh_token,
            expires_in: tokenData.expires_in,
        });

    } catch (error) {
        logger.error({ err: error }, 'Login API error');
        return NextResponse.json(
            { error: 'An unexpected error occurred. Please try again.' },
            { status: 500 }
        );
    }
}

