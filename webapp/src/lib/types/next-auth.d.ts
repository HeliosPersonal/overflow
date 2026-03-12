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
        } & DefaultUser;
        accessToken: string;
    }

    interface User {
        id: string;
        displayName: string;
        reputation: number;
        roles: string[];
        isAnonymous?: boolean;
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
        };
    }
}