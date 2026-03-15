import NextAuth from "next-auth"
import Keycloak from "next-auth/providers/keycloak"
import Credentials from "next-auth/providers/credentials"
import {apiConfig, authConfig} from "@/lib/config";
import { loginSchema } from "@/lib/validators/auth";
import { isAnonymousEmail } from "@/lib/keycloak-admin";
import { cookies } from "next/headers";

export const { handlers, signIn, signOut, auth } = NextAuth({
    debug: false,
    trustHost: true,
    logger: {
        error(error) {
            // JWTSessionError means the browser has a stale cookie encrypted with a
            // different AUTH_SECRET (e.g. left from staging). Treat as unauthenticated —
            // the cookie is cleared automatically on next sign-in. Log at debug level only.
            if (error.name === 'JWTSessionError') {
                console.debug('[Auth] Stale session cookie detected (JWTSessionError) — treating as unauthenticated');
                return;
            }
            console.error('[Auth] Error:', error);
        },
        warn(code) { console.warn('[Auth] Warning:', code); },
        debug(code, metadata) { console.debug('[Auth] Debug:', code, metadata); },
    },
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
                        console.error('[Auth] Credential validation failed:', validatedFields.error);
                        return null;
                    }

                    const { email, password } = validatedFields.data;

                    // Authenticate with Keycloak using Direct Access Grant
                    const tokenUrl = `${authConfig.kcInternal}/protocol/openid-connect/token`;
                    
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

                    if (!tokenResponse.ok) {
                        const errorData = await tokenResponse.text();
                        console.error('[Auth] Keycloak authentication failed:', tokenResponse.status, errorData);
                        return null;
                    }

                    const tokens = await tokenResponse.json();

                    // Get user info
                    const userInfoUrl = `${authConfig.kcInternal}/protocol/openid-connect/userinfo`;
                    
                    const userInfoResponse = await fetch(userInfoUrl, {
                        headers: {
                            Authorization: `Bearer ${tokens.access_token}`,
                        },
                    });

                    if (!userInfoResponse.ok) {
                        console.error('[Auth] Failed to fetch user info:', userInfoResponse.status);
                        return null;
                    }

                    const userInfo = await userInfoResponse.json();

                    // Fetch user profile — optional, fall back to defaults if backend unavailable
                    const profileUrl = `${apiConfig.baseUrl}/profiles/me`;

                    let profile = null;
                    try {
                        const profileResponse = await fetch(profileUrl, {
                            headers: { Authorization: `Bearer ${tokens.access_token}` }
                        });
                        if (profileResponse.ok) {
                            profile = await profileResponse.json();
                        }
                    } catch (profileError) {
                        console.warn('[Auth] Profile fetch failed, proceeding with defaults:',
                            profileError instanceof Error ? profileError.message : profileError);
                    }

                    const isAnonymous = isAnonymousEmail(userInfo.email);

                    // Resolve display name:
                    //   1. ProfileService (source of truth for registered users)
                    //   2. For anonymous users: Keycloak given_name (= the guest name they entered)
                    //   3. Keycloak preferred_username (email for regular users)
                    //   4. Keycloak full name (firstName + lastName)
                    const displayName = profile?.displayName
                        || (isAnonymous ? (userInfo.given_name || userInfo.name) : null)
                        || userInfo.preferred_username
                        || userInfo.name;

                    const userObject = {
                        id: userInfo.sub,
                        email: userInfo.email,
                        name: userInfo.name,
                        displayName,
                        reputation: profile?.reputation || 0,
                        isAnonymous,
                        emailVerified: userInfo.email_verified ?? false,
                        avatarUrl: profile?.avatarUrl ?? null,
                        roles: (() => {
                            try {
                                const payload = JSON.parse(Buffer.from(tokens.access_token.split('.')[1], 'base64').toString());
                                return payload?.realm_access?.roles ?? [];
                            } catch { return []; }
                        })(),
                        accessToken: tokens.access_token,
                        refreshToken: tokens.refresh_token,
                        accessTokenExpires: Math.floor(Date.now() / 1000) + tokens.expires_in,
                    };
                    
                    return userObject;
                } catch (error) {
                    console.error('[Auth] Authorize error:', error instanceof Error ? error.message : String(error));
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
        async signIn({user, account}) {
            // Block credentials login for non-anonymous users who haven't verified their email.
            // Anonymous (guest) users always have emailVerified=true on their placeholder email.
            // Keycloak SSO (provider='keycloak') handles verification on its own.
            if (account?.provider === 'credentials') {
                const u = user as typeof user & { emailVerified?: boolean; isAnonymous?: boolean };
                if (!u.isAnonymous && u.emailVerified === false) {
                    console.warn('[Auth] Login blocked — email not verified:', user.email);
                    return '/login?error=EMAIL_NOT_VERIFIED';
                }
            }
            return true;
        },
        async jwt({token, account, user}) {
            try {
                const now = Math.floor(Date.now() / 1000);
                
                // Initial sign in with Credentials provider
                if (account?.provider === 'credentials' && user) {
                    token.user = {
                        id: user.id,
                        displayName: user.displayName,
                        reputation: user.reputation,
                        roles: (user as typeof user & { roles: string[] }).roles ?? [],
                        email: user.email || '',
                        emailVerified: new Date(),
                        isAnonymous: user.isAnonymous,
                        avatarUrl: user.avatarUrl ?? null,
                    };
                    
                    token.accessToken = (user as typeof user & { accessToken: string }).accessToken;
                    token.refreshToken = (user as typeof user & { refreshToken: string }).refreshToken;
                    token.accessTokenExpires = (user as typeof user & { accessTokenExpires: number }).accessTokenExpires;
                    token.profileLastFetched = now;
                    token.error = undefined;
                    return token;
                }
            
                // Initial sign in with Keycloak provider
                if (account?.provider === 'keycloak' && account.access_token && account.refresh_token) {
                    let profileData = null;
                    try {
                        const res = await fetch(apiConfig.baseUrl + '/profiles/me', {
                            headers: { Authorization: `Bearer ${account.access_token}` }
                        });
                        if (res.ok) {
                            profileData = await res.json();
                        }
                    } catch (profileError) {
                        console.warn('[Auth] Profile fetch failed, using defaults:',
                            profileError instanceof Error ? profileError.message : profileError);
                    }

                    const kcRoles = (() => {
                        try {
                            const payload = JSON.parse(Buffer.from(account.access_token.split('.')[1], 'base64').toString());
                            return payload?.realm_access?.roles ?? [];
                        } catch { return []; }
                    })();

                    token.user = profileData
                        ? { ...profileData, id: profileData.userId ?? profileData.id, roles: kcRoles }
                        : { id: '', displayName: '', reputation: 0, roles: kcRoles, email: '', emailVerified: null, avatarUrl: null };
                    token.accessToken = account.access_token
                    token.refreshToken = account.refresh_token;
                    token.accessTokenExpires = now + account.expires_in!;
                    token.profileLastFetched = now;
                    token.error = undefined;
                    return token;
                }
            
                // Token still valid — but periodically re-fetch profile from ProfileService
                // (source of truth for displayName/reputation) so session stays current.
                // Also re-fetch immediately if avatarUrl is missing (just saved but not in session yet).
                // Also re-fetch immediately if the profile_dirty cookie is set (profile was just edited).
                if (token.accessTokenExpires && now < token.accessTokenExpires) {
                    const PROFILE_REFRESH_INTERVAL = 60; // seconds
                    const lastFetched = token.profileLastFetched ?? 0;
                    const avatarMissing = !token.user?.avatarUrl;

                    let profileDirty = false;
                    try {
                        const cookieStore = await cookies();
                        profileDirty = cookieStore.get('profile_dirty')?.value === '1';
                    } catch { /* edge runtime or non-request context */ }
                    
                    if ((profileDirty || avatarMissing || now - lastFetched >= PROFILE_REFRESH_INTERVAL) && token.accessToken) {
                        try {
                            const profileRes = await fetch(apiConfig.baseUrl + '/profiles/me', {
                                headers: { Authorization: `Bearer ${token.accessToken}` }
                            });
                            if (profileRes.ok) {
                                const profile = await profileRes.json();
                                if (token.user) {
                                    token.user.displayName = profile.displayName ?? token.user.displayName;
                                    token.user.reputation = profile.reputation ?? token.user.reputation;
                                    token.user.avatarUrl = profile.avatarUrl !== undefined ? profile.avatarUrl : token.user.avatarUrl;
                                }
                            }
                        } catch {
                            // Non-fatal — keep existing session values
                        }
                        token.profileLastFetched = now;

                        // Clear the dirty flag after re-fetching
                        if (profileDirty) {
                            try {
                                const cookieStore = await cookies();
                                cookieStore.delete('profile_dirty');
                            } catch { /* ignore */ }
                        }
                    }
                    
                    return token;
                }
            
                // Token expired — refresh
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
                        console.error('[Auth] Failed to refresh token:', refreshed);
                        token.error = 'RefreshAccessTokenError';
                        return token;
                    }
                    
                    token.accessToken = refreshed.access_token;
                    token.refreshToken = refreshed.refresh_token;
                    token.accessTokenExpires = now + refreshed.expires_in!;

                    // Re-fetch profile from ProfileService on every token refresh
                    // so displayName/reputation stay current (ProfileService = source of truth)
                    try {
                        const profileRes = await fetch(apiConfig.baseUrl + '/profiles/me', {
                            headers: { Authorization: `Bearer ${refreshed.access_token}` }
                        });
                        if (profileRes.ok) {
                            const profile = await profileRes.json();
                            if (token.user) {
                                token.user.displayName = profile.displayName ?? token.user.displayName;
                                token.user.reputation = profile.reputation ?? token.user.reputation;
                                token.user.avatarUrl = profile.avatarUrl !== undefined ? profile.avatarUrl : token.user.avatarUrl;
                            }
                        }
                    } catch (profileError) {
                        // Non-fatal — keep existing session values
                        console.warn('[Auth] Profile re-fetch on refresh failed:',
                            profileError instanceof Error ? profileError.message : profileError);
                    }
                    token.profileLastFetched = now;
                } catch (error) {
                    console.error('[Auth] Token refresh error:', error);
                    token.error = 'RefreshAccessTokenError';
                }
            
                return token;
            } catch (error) {
                console.error('[Auth] JWT callback error:', error instanceof Error ? error.message : String(error));
                return token;
            }
        },
        async session({session, token}) {
            if (token.user) {
                session.user = {
                    ...token.user,
                    roles: token.user.roles ?? [],
                };
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