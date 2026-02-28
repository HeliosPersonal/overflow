import { NextRequest, NextResponse } from 'next/server';
import { authConfig } from '@/lib/config';

export async function POST(request: NextRequest) {
    try {
        const { email, username, firstName, lastName, password } = await request.json();

        if (!email || !username || !firstName || !lastName || !password) {
            return NextResponse.json(
                { error: 'All fields are required' },
                { status: 400 }
            );
        }

        console.log('Signup request for:', email, username);

        // Get admin access token to create user (uses service account client, not the user-facing client)
        const tokenUrl = `${authConfig.kcInternal}/protocol/openid-connect/token`;
        console.log('Getting admin token from:', tokenUrl);
        console.log('Admin Client ID:', authConfig.kcAdminClientId);
        console.log('Has admin client secret:', !!authConfig.kcAdminClientSecret);
        
        const adminTokenResponse = await fetch(tokenUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: new URLSearchParams({
                grant_type: 'client_credentials',
                client_id: authConfig.kcAdminClientId,
                client_secret: authConfig.kcAdminClientSecret,
            }),
        });

        console.log('Token response status:', adminTokenResponse.status);

        if (!adminTokenResponse.ok) {
            const error = await adminTokenResponse.json();
            console.error('Failed to get admin token:', error);
            console.error('This usually means:');
            console.error('1. Client authentication is not enabled');
            console.error('2. Client secret is wrong');
            console.error('3. Client does not exist');
            return NextResponse.json(
                { error: 'Registration service temporarily unavailable' },
                { status: 503 }
            );
        }

        const adminToken = await adminTokenResponse.json();
        console.log('Admin token obtained successfully');
        
        // Debug: Check what's in the token
        try {
            const tokenParts = adminToken.access_token.split('.');
            const payload = JSON.parse(Buffer.from(tokenParts[1], 'base64').toString());
            console.log('Token payload:', {
                resource_access: payload.resource_access,
                realm_access: payload.realm_access,
                scope: payload.scope,
                clientId: payload.azp || payload.client_id
            });
        } catch (e) {
            console.log('Could not decode token for debugging');
        }

        // Extract base URL and realm name
        const { baseUrl, realmName } = extractKeycloakConfig();
        const createUserUrl = `${baseUrl}/admin/realms/${realmName}/users`;
        
        console.log('Creating user at:', createUserUrl);
        console.log('Realm:', realmName);

        // Create user in Keycloak
        const createUserResponse = await fetch(createUserUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${adminToken.access_token}`,
            },
            body: JSON.stringify({
                username: username,
                email: email,
                firstName: firstName,
                lastName: lastName,
                enabled: true,
                emailVerified: false,
                credentials: [
                    {
                        type: 'password',
                        value: password,
                        temporary: false,
                    },
                ],
            }),
        });

        if (!createUserResponse.ok) {
            const errorText = await createUserResponse.text();
            console.error('Failed to create user:', errorText, 'Status:', createUserResponse.status);
            
            // Check for duplicate user
            if (createUserResponse.status === 409) {
                return NextResponse.json(
                    { error: 'User with this email or username already exists' },
                    { status: 409 }
                );
            }
            
            // Check for permission error
            if (createUserResponse.status === 403) {
                console.error('Permission denied: Service account needs manage-users role');
                return NextResponse.json(
                    { error: 'Registration is temporarily unavailable. Please try again later.' },
                    { status: 503 }
                );
            }
            
            return NextResponse.json(
                { error: 'Failed to create user account' },
                { status: createUserResponse.status }
            );
        }


        // User created successfully
        return NextResponse.json({
            message: 'Account created successfully',
            email: email,
        }, { status: 201 });

    } catch (error) {
        console.error('Signup API error:', error);
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

