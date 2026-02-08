import NextAuth from "next-auth"
import Keycloak from "next-auth/providers/keycloak"
import Credentials from "next-auth/providers/credentials"
import {apiConfig, authConfig} from "@/lib/config";
import { loginSchema } from "@/lib/validators/auth";

export const { handlers, signIn, signOut, auth } = NextAuth({
    pages: {
        signIn: '/login',
        error: '/login',
    },
    providers: [
        Credentials({
            name: 'credentials',
            credentials: {
                email: { label: "Email", type: "email" },
                password: { label: "Password", type: "password" }
            },
            async authorize(credentials) {
                try {
                    const validatedFields = loginSchema.safeParse(credentials);
                    
                    if (!validatedFields.success) {
                        console.error('Credential validation failed');
                        return null;
                    }

                    const { email, password } = validatedFields.data;

                    // Authenticate with Keycloak using Direct Access Grant
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

                    if (!tokenResponse.ok) {
                        const errorData = await tokenResponse.text();
                        console.error('Keycloak authentication failed:', errorData);
                        return null;
                    }

                    const tokens = await tokenResponse.json();

                    // Get user info
                    const userInfoResponse = await fetch(
                        `${authConfig.kcInternal}/protocol/openid-connect/userinfo`,
                        {
                            headers: {
                                Authorization: `Bearer ${tokens.access_token}`,
                            },
                        }
                    );

                    if (!userInfoResponse.ok) {
                        console.error('Failed to fetch user info');
                        return null;
                    }

                    const userInfo = await userInfoResponse.json();

                    // Fetch user profile
                    const profileResponse = await fetch(apiConfig.baseUrl + '/profiles/me', {
                        headers: {
                            Authorization: `Bearer ${tokens.access_token}`,
                        }
                    });

                    let profile = null;
                    if (profileResponse.ok) {
                        profile = await profileResponse.json();
                    } else {
                        console.warn('Profile fetch failed, using defaults');
                    }

                    return {
                        id: userInfo.sub,
                        email: userInfo.email,
                        name: userInfo.name,
                        displayName: profile?.displayName || userInfo.preferred_username || userInfo.name,
                        reputation: profile?.reputation || 0,
                        accessToken: tokens.access_token,
                        refreshToken: tokens.refresh_token,
                        accessTokenExpires: Math.floor(Date.now() / 1000) + tokens.expires_in,
                    };
                } catch (error) {
                    console.error('Authorization error:', error);
                    return null;
                }
            }
        }),
        Keycloak({
            authorization: {
                params: {scope: 'openid profile email offline_access'},
                url: `${authConfig.kcIssuer}/protocol/openid-connect/auth`
            },
            token: `${authConfig.kcInternal}/protocol/openid-connect/token`,
            userinfo: `${authConfig.kcInternal}/protocol/openid-connect/userinfo`,
        })
    ],
    callbacks: {
        async jwt({token, account, user}) {
            const now = Math.floor(Date.now() / 1000);
            
            // Initial sign in with Credentials provider
            if (account?.provider === 'credentials' && user) {
                token.user = {
                    id: user.id,
                    displayName: user.displayName,
                    reputation: user.reputation,
                    email: user.email || '',
                    emailVerified: new Date(),
                };
                
                token.accessToken = (user as typeof user & { accessToken: string }).accessToken;
                token.refreshToken = (user as typeof user & { refreshToken: string }).refreshToken;
                token.accessTokenExpires = (user as typeof user & { accessTokenExpires: number }).accessTokenExpires;
                token.error = undefined;
                return token;
            }
            
            // Initial sign in with Keycloak provider
            if (account?.provider === 'keycloak' && account.access_token && account.refresh_token) {
                const res = await fetch(apiConfig.baseUrl + '/profiles/me', {
                    headers: {
                        Authorization: `Bearer ${account.access_token}`,
                    }
                })
                
                if (res.ok) {
                    token.user = await res.json()
                } else {
                    console.error('Failed to fetch user profile:', await res.text())
                }
                
                token.accessToken = account.access_token
                token.refreshToken = account.refresh_token;
                token.accessTokenExpires = now + account.expires_in!;
                token.error = undefined;
                return token;
            }
            
            if (token.accessTokenExpires && now < token.accessTokenExpires) {
                return token;
            }
            
            try {
                const response = await fetch(`${authConfig.kcInternal}/protocol/openid-connect/token`, {
                    method: 'POST',
                    headers: {'Content-Type': 'application/x-www-form-urlencoded'},
                    body: new URLSearchParams({
                        grant_type: 'refresh_token',
                        client_id: authConfig.kcClientId,
                        client_secret: authConfig.kcSecret,
                        refresh_token: token.refreshToken as string
                    })
                })
                
                const refreshed = await response.json()
                
                if (!response.ok) {
                    console.log('Failed to refresh token', refreshed);
                    token.error = 'RefreshAccessTokenError';
                    return token;
                }
                
                token.accessToken = refreshed.access_token;
                token.refreshToken = refreshed.refresh_token;
                token.accessTokenExpires = now + refreshed.expires_in!;
            } catch (error) {
                console.log('Failed to refresh token', error);
                token.error = 'RefreshAccessTokenError';
            }
            
            return token;
        },
        async session({session, token}) {
            if (token.user) {
                session.user = token.user
            }
            
            if (token.accessToken) {
                session.accessToken = token.accessToken;
            }
            
            if (token.accessTokenExpires) {
                session.expires = new Date(token.accessTokenExpires * 1000) as unknown as typeof session.expires;
            }
            return session;
        }
    }
})