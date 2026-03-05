import NextAuth from "next-auth"
import Keycloak from "next-auth/providers/keycloak"
import Credentials from "next-auth/providers/credentials"
import {apiConfig, authConfig} from "@/lib/config";
import { loginSchema } from "@/lib/validators/auth";

export const { handlers, signIn, signOut, auth } = NextAuth({
    debug: true, // Enable detailed NextAuth logging
    trustHost: true,
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
                console.log('[Auth] ========== AUTHORIZE START ==========');
                console.log('[Auth] Timestamp:', new Date().toISOString());
                console.log('[Auth] Credentials received:', { 
                    hasEmail: !!credentials?.email, 
                    hasPassword: !!credentials?.password 
                });
                
                try {
                    const validatedFields = loginSchema.safeParse(credentials);
                    
                    if (!validatedFields.success) {
                        console.error('[Auth] Credential validation failed:', validatedFields.error);
                        console.log('[Auth] ========== AUTHORIZE END (VALIDATION FAILED) ==========');
                        return null;
                    }

                    const { email, password } = validatedFields.data;
                    console.log('[Auth] Credentials validated for:', email);

                    // Authenticate with Keycloak using Direct Access Grant
                    const tokenUrl = `${authConfig.kcInternal}/protocol/openid-connect/token`;
                    console.log('[Auth] Calling Keycloak token endpoint:', tokenUrl);
                    console.log('[Auth] Client ID:', authConfig.kcClientId);
                    
                    const tokenResponse = await fetch(tokenUrl, {
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
                    });
                    
                    console.log('[Auth] Token response status:', tokenResponse.status);

                    if (!tokenResponse.ok) {
                        const errorData = await tokenResponse.text();
                        console.error('[Auth] Keycloak authentication failed');
                        console.error('[Auth] Status:', tokenResponse.status);
                        console.error('[Auth] Error:', errorData);
                        console.log('[Auth] ========== AUTHORIZE END (KC AUTH FAILED) ==========');
                        return null;
                    }

                    const tokens = await tokenResponse.json();
                    console.log('[Auth] Tokens received successfully');
                    console.log('[Auth] Token expires in:', tokens.expires_in, 'seconds');

                    // Get user info
                    const userInfoUrl = `${authConfig.kcInternal}/protocol/openid-connect/userinfo`;
                    console.log('[Auth] Fetching user info from:', userInfoUrl);
                    
                    const userInfoResponse = await fetch(userInfoUrl, {
                        headers: {
                            Authorization: `Bearer ${tokens.access_token}`,
                        },
                    });
                    
                    console.log('[Auth] User info response status:', userInfoResponse.status);

                    if (!userInfoResponse.ok) {
                        console.error('[Auth] Failed to fetch user info');
                        console.error('[Auth] Status:', userInfoResponse.status);
                        console.log('[Auth] ========== AUTHORIZE END (USERINFO FAILED) ==========');
                        return null;
                    }

                    const userInfo = await userInfoResponse.json();
                    console.log('[Auth] User info received:', { 
                        sub: userInfo.sub, 
                        email: userInfo.email,
                        name: userInfo.name 
                    });

                    // Fetch user profile
                    const profileUrl = `${apiConfig.baseUrl}/profiles/me`;
                    console.log('[Auth] Fetching profile from:', profileUrl);
                    
                    const profileResponse = await fetch(profileUrl, {
                        headers: {
                            Authorization: `Bearer ${tokens.access_token}`,
                        }
                    });
                    
                    console.log('[Auth] Profile response status:', profileResponse.status);

                    let profile = null;
                    if (profileResponse.ok) {
                        profile = await profileResponse.json();
                        console.log('[Auth] Profile fetched successfully:', { 
                            displayName: profile?.displayName, 
                            reputation: profile?.reputation 
                        });
                    } else {
                        console.warn('[Auth] Profile fetch failed, using defaults');
                    }

                    const userObject = {
                        id: userInfo.sub,
                        email: userInfo.email,
                        name: userInfo.name,
                        displayName: profile?.displayName || userInfo.preferred_username || userInfo.name,
                        reputation: profile?.reputation || 0,
                        accessToken: tokens.access_token,
                        refreshToken: tokens.refresh_token,
                        accessTokenExpires: Math.floor(Date.now() / 1000) + tokens.expires_in,
                    };
                    
                    console.log('[Auth] User object created:', { 
                        id: userObject.id, 
                        email: userObject.email,
                        displayName: userObject.displayName,
                        reputation: userObject.reputation,
                        hasAccessToken: !!userObject.accessToken,
                        hasRefreshToken: !!userObject.refreshToken,
                        accessTokenExpires: userObject.accessTokenExpires
                    });
                    console.log('[Auth] ========== AUTHORIZE END (SUCCESS) ==========');
                    
                    return userObject;
                } catch (error) {
                    console.error('[Auth] ========== AUTHORIZE ERROR ==========');
                    console.error('[Auth] Error type:', error?.constructor?.name);
                    console.error('[Auth] Error message:', error instanceof Error ? error.message : String(error));
                    console.error('[Auth] Error stack:', error instanceof Error ? error.stack : 'N/A');
                    console.log('[Auth] ========== AUTHORIZE END (ERROR) ==========');
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
            console.log('[JWT Callback] ========== JWT CALLBACK START ==========');
            console.log('[JWT Callback] Timestamp:', new Date().toISOString());
            console.log('[JWT Callback] Has account:', !!account);
            console.log('[JWT Callback] Has user:', !!user);
            console.log('[JWT Callback] Has token:', !!token);
            console.log('[JWT Callback] Provider:', account?.provider);
            
            try {
                const now = Math.floor(Date.now() / 1000);
                
                // Initial sign in with Credentials provider
                if (account?.provider === 'credentials' && user) {
                    console.log('[JWT Callback] Processing credentials provider sign-in');
                    console.log('[JWT Callback] User data:', {
                        id: user.id,
                        email: user.email,
                        displayName: user.displayName,
                        reputation: user.reputation
                    });
                    
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
                    
                    console.log('[JWT Callback] Token created successfully');
                    console.log('[JWT Callback] Token expires at:', new Date(token.accessTokenExpires * 1000).toISOString());
                    console.log('[JWT Callback] ========== JWT CALLBACK END (CREDENTIALS SUCCESS) ==========');
                    return token;
                }
            
            // Initial sign in with Keycloak provider
            if (account?.provider === 'keycloak' && account.access_token && account.refresh_token) {
                console.log('[JWT Callback] Processing Keycloak provider sign-in');

                const res = await fetch(apiConfig.baseUrl + '/profiles/me', {
                    headers: { Authorization: `Bearer ${account.access_token}` }
                });

                if (res.ok) {
                    token.user = await res.json();
                    console.log('[JWT Callback] Profile fetched for Keycloak user');
                } else {
                    console.error('[JWT Callback] Failed to fetch user profile:', await res.text());
                }

                token.accessToken = account.access_token
                token.refreshToken = account.refresh_token;
                token.accessTokenExpires = now + account.expires_in!;
                token.error = undefined;
                
                console.log('[JWT Callback] ========== JWT CALLBACK END (KEYCLOAK SUCCESS) ==========');
                return token;
            }
            
            if (token.accessTokenExpires && now < token.accessTokenExpires) {
                console.log('[JWT Callback] Token still valid, no refresh needed');
                console.log('[JWT Callback] ========== JWT CALLBACK END (TOKEN VALID) ==========');
                return token;
            }
            
            console.log('[JWT Callback] Token expired, attempting refresh');
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
                
                console.log('[JWT Callback] Refresh response status:', response.status);
                const refreshed = await response.json()
                
                if (!response.ok) {
                    console.error('[JWT Callback] Failed to refresh token:', refreshed);
                    token.error = 'RefreshAccessTokenError';
                    console.log('[JWT Callback] ========== JWT CALLBACK END (REFRESH FAILED) ==========');
                    return token;
                }
                
                console.log('[JWT Callback] Token refreshed successfully');
                token.accessToken = refreshed.access_token;
                token.refreshToken = refreshed.refresh_token;
                token.accessTokenExpires = now + refreshed.expires_in!;
            } catch (error) {
                console.error('[JWT Callback] Token refresh error:', error);
                token.error = 'RefreshAccessTokenError';
            }
            
            console.log('[JWT Callback] ========== JWT CALLBACK END (REFRESH COMPLETE) ==========');
            return token;
            } catch (error) {
                console.error('[JWT Callback] ========== JWT CALLBACK ERROR ==========');
                console.error('[JWT Callback] Error type:', error?.constructor?.name);
                console.error('[JWT Callback] Error message:', error instanceof Error ? error.message : String(error));
                console.error('[JWT Callback] Error stack:', error instanceof Error ? error.stack : 'N/A');
                console.log('[JWT Callback] ========== JWT CALLBACK END (ERROR) ==========');
                // Return token as-is to prevent complete auth failure
                return token;
            }
        },
        async session({session, token}) {
            console.log('[Session Callback] ========== SESSION CALLBACK START ==========');
            console.log('[Session Callback] Timestamp:', new Date().toISOString());
            console.log('[Session Callback] Has token:', !!token);
            console.log('[Session Callback] Has token.user:', !!token.user);
            console.log('[Session Callback] Has session:', !!session);
            
            try {
                if (token.user) {
                    session.user = token.user;
                    console.log('[Session Callback] User added to session:', {
                        id: token.user.id,
                        email: token.user.email,
                        displayName: token.user.displayName
                    });
                }
                
                if (token.accessToken) {
                    session.accessToken = token.accessToken;
                    console.log('[Session Callback] Access token added to session');
                }
                
                if (token.accessTokenExpires) {
                    session.expires = new Date(token.accessTokenExpires * 1000) as unknown as typeof session.expires;
                    console.log('[Session Callback] Session expires at:', session.expires);
                }
                
                console.log('[Session Callback] ========== SESSION CALLBACK END (SUCCESS) ==========');
                return session;
            } catch (error) {
                console.error('[Session Callback] ========== SESSION CALLBACK ERROR ==========');
                console.error('[Session Callback] Error type:', error?.constructor?.name);
                console.error('[Session Callback] Error message:', error instanceof Error ? error.message : String(error));
                console.error('[Session Callback] Error stack:', error instanceof Error ? error.stack : 'N/A');
                console.log('[Session Callback] ========== SESSION CALLBACK END (ERROR) ==========');
                return session;
            }
        }
    }
})