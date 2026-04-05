// These imports are required to enable module augmentation for next-auth
import 'next-auth';
import 'next-auth/jwt';
import {DefaultUser} from "@auth/core/types";

declare module 'next-auth' {
    interface Session {
        user: {
            id: string;
            displayName: string;
            reputation: number;
            roles: string[];
            isAnonymous?: boolean;
            avatarUrl?: string | null;
        } & DefaultUser;
        accessToken: string;
        /** Set to 'RefreshAccessTokenError' when the Keycloak refresh token is invalid
         *  (expired session, pod restart, admin revocation). Middleware detects this and
         *  signs the user out so they are redirected to login. */
        error?: 'RefreshAccessTokenError';
    }

    interface User {
        id: string;
        displayName: string;
        reputation: number;
        roles: string[];
        email?: string | null;
        emailVerified?: Date | null;
        isAnonymous?: boolean;
        avatarUrl?: string | null;
    }
}

declare module 'next-auth/jwt' {
    interface JWT {
        accessToken: string;
        refreshToken: string;
        accessTokenExpires: number;
        profileLastFetched?: number;
        error?: string;
        user: {
            id: string;
            displayName: string;
            reputation: number;
            roles: string[];
            email: string;
            emailVerified: Date | null;
            isAnonymous?: boolean;
            avatarUrl?: string | null;
        };
    }
}