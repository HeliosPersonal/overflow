import NextAuth from "next-auth"
import Keycloak from "next-auth/providers/keycloak"
import Credentials from "next-auth/providers/credentials"
import {apiConfig, authConfig} from "@/lib/config";
import {loginSchema} from "@/lib/validators/auth";
import {isAnonymousEmail} from "@/lib/keycloak-admin";
import { createLogger } from "@/lib/logger";

const logger = createLogger('auth');

export const {handlers, signIn, signOut, auth} = NextAuth({
    debug: false,
    trustHost: true,
    logger: {
        error(error) {
            if (error.name === 'JWTSessionError') {
                logger.debug('Stale session cookie detected (JWTSessionError) — treating as unauthenticated');
                return;
            }
            logger.error({err: error}, 'Auth error');
        },
        warn(code) {
            logger.warn({code}, 'Auth warning');
        },
        debug(code, metadata) {
            logger.debug({code, metadata}, 'Auth debug');
        },
    },
    pages: {
        signIn: '/login',
        error: '/login',
    },
    providers: [
        Credentials({
            name: 'credentials',
            credentials: {
                email: {label: "Email", type: "email"},
                password: {label: "Password", type: "password"}
            },
            async authorize(credentials) {
                try {
                    const validatedFields = loginSchema.safeParse(credentials);

                    if (!validatedFields.success) {
                        logger.error({err: validatedFields.error}, 'Credential validation failed');
                        return null;
                    }

                    const {email, password} = validatedFields.data;

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
                        logger.error({status: tokenResponse.status, body: errorData}, 'Keycloak authentication failed');
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
                        logger.error({status: userInfoResponse.status}, 'Failed to fetch user info');
                        return null;
                    }

                    const userInfo = await userInfoResponse.json();

                    // Fetch user profile — optional, fall back to defaults if backend unavailable
                    const profileUrl = `${apiConfig.baseUrl}/profiles/me`;

                    let profile = null;
                    try {
                        const profileResponse = await fetch(profileUrl, {
                            headers: {Authorization: `Bearer ${tokens.access_token}`}
                        });
                        if (profileResponse.ok) {
                            profile = await profileResponse.json();
                        }
                    } catch (profileError) {
                        logger.warn({err: profileError}, 'Profile fetch failed, proceeding with defaults');
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
                        // Anonymous users have emailVerified=true in Keycloak only to satisfy
                        // Direct Access Grant — their placeholder email is not actually verified.
                        // Use null (not false) — next-auth's User type uses Date|null.
                        emailVerified: isAnonymous ? null : (userInfo.email_verified ? new Date() : null),
                        avatarUrl: profile?.avatarUrl ?? null,
                        roles: (() => {
                            try {
                                const payload = JSON.parse(Buffer.from(tokens.access_token.split('.')[1], 'base64').toString());
                                return payload?.realm_access?.roles ?? [];
                            } catch {
                                return [];
                            }
                        })(),
                        accessToken: tokens.access_token,
                        refreshToken: tokens.refresh_token,
                        accessTokenExpires: Math.floor(Date.now() / 1000) + tokens.expires_in,
                    };

                    return userObject;
                } catch (error) {
                    logger.error({err: error}, 'Authorize error');
                    return null;
                }
            }
        }),
        Keycloak({
            clientId: authConfig.kcClientId,
            clientSecret: authConfig.kcSecret,
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
            // Anonymous (guest) users are always allowed through — their emailVerified is null
            // but they must not be blocked (they have no real email to verify).
            // Keycloak SSO (provider='keycloak') handles verification on its own.
            if (account?.provider === 'credentials') {
                const u = user as typeof user & { emailVerified?: Date | null; isAnonymous?: boolean };
                if (!u.isAnonymous && !u.emailVerified) {
                    logger.warn({email: user.email}, 'Login blocked — email not verified');
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
                        emailVerified: user.emailVerified ?? null,
                        isAnonymous: user.isAnonymous,
                        avatarUrl: user.avatarUrl ?? null,
                    };

                    token.accessToken = (user as typeof user & { accessToken: string }).accessToken;
                    token.refreshToken = (user as typeof user & { refreshToken: string }).refreshToken;
                    token.accessTokenExpires = (user as typeof user & {
                        accessTokenExpires: number
                    }).accessTokenExpires;
                    token.profileLastFetched = now;
                    token.error = undefined;
                    return token;
                }

                // Initial sign in with Keycloak provider
                if (account?.provider === 'keycloak' && account.access_token && account.refresh_token) {
                    let profileData = null;
                    try {
                        const res = await fetch(apiConfig.baseUrl + '/profiles/me', {
                            headers: {Authorization: `Bearer ${account.access_token}`}
                        });
                        if (res.ok) {
                            profileData = await res.json();
                        }
                    } catch (profileError) {
                        logger.warn({err: profileError}, 'Profile fetch failed, using defaults');
                    }

                    const kcRoles = (() => {
                        try {
                            const payload = JSON.parse(Buffer.from(account.access_token.split('.')[1], 'base64').toString());
                            return payload?.realm_access?.roles ?? [];
                        } catch {
                            return [];
                        }
                    })();

                    token.user = profileData
                        ? {...profileData, id: profileData.userId ?? profileData.id, roles: kcRoles}
                        : {
                            id: '',
                            displayName: '',
                            reputation: 0,
                            roles: kcRoles,
                            email: '',
                            emailVerified: null,
                            avatarUrl: null
                        };
                    token.accessToken = account.access_token
                    token.refreshToken = account.refresh_token;
                    token.accessTokenExpires = now + account.expires_in!;
                    token.profileLastFetched = now;
                    token.error = undefined;
                    return token;
                }

                // Token still valid — but periodically re-fetch profile from ProfileService
                // so session data (displayName/reputation) stays reasonably current.
                // Mutable profile data displayed in the UI (UserMenu) is fetched directly
                // from ProfileService in TopNav — this is just for session consistency.
                if (token.accessTokenExpires && now < token.accessTokenExpires) {
                    const PROFILE_REFRESH_INTERVAL = 60; // seconds
                    const lastFetched = token.profileLastFetched ?? 0;

                    if ((now - lastFetched >= PROFILE_REFRESH_INTERVAL) && token.accessToken) {
                        try {
                            const profileRes = await fetch(apiConfig.baseUrl + '/profiles/me', {
                                headers: {Authorization: `Bearer ${token.accessToken}`}
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
                        logger.error({body: refreshed}, 'Failed to refresh token');
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
                            headers: {Authorization: `Bearer ${refreshed.access_token}`}
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
                        logger.warn({err: profileError}, 'Profile re-fetch on refresh failed');
                    }
                    token.profileLastFetched = now;
                } catch (error) {
                    logger.error({err: error}, 'Token refresh error');
                    token.error = 'RefreshAccessTokenError';
                }

                return token;
            } catch (error) {
                logger.error({err: error}, 'JWT callback error');
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

            // Propagate refresh error so middleware can detect it and sign the user out.
            // Without this the client has no idea the session is broken and all backend
            // calls silently return 401 until the NextAuth cookie eventually expires.
            if (token.error) {
                session.error = token.error as 'RefreshAccessTokenError';
            }

            return session;
        }
    }
})