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
        } & DefaultUser;
        accessToken: string;
    }

    interface User {
        id: string;
        displayName: string;
        reputation: number;
        roles: string[];
    }
}

declare module 'next-auth/jwt' {
    interface JWT {
        accessToken: string;
        refreshToken: string;
        accessTokenExpires: number;
        error?: string;
        user: {
            id: string;
            displayName: string;
            reputation: number;
            roles: string[];
            email: string;
            emailVerified: Date | null;
        };
    }
}