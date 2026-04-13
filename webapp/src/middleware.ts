import {auth} from "@/auth";
import {NextResponse} from "next/server";

export default auth((req) => {
    const { nextUrl, auth: session } = req;

    // Keycloak refresh token is no longer valid (session expired, pod restart, admin
    // revocation, or similar). Clear the NextAuth session cookie and redirect to login
    // so the user re-authenticates instead of being stuck with a permanently broken session
    // where all API calls silently fail with 401.
    if (session?.error === 'RefreshAccessTokenError') {
        const url = new URL('/login?error=SESSION_EXPIRED', nextUrl);
        const response = NextResponse.redirect(url);
        // Delete both the HTTP and Secure (HTTPS) variants of the NextAuth session cookie
        response.cookies.delete('authjs.session-token');
        response.cookies.delete('__Secure-authjs.session-token');
        return response;
    }

    if (session) {
        return NextResponse.next();
    }

    const callbackUrl = encodeURIComponent(nextUrl.pathname + nextUrl.search);
    return NextResponse.redirect(new URL(`/auth-gate?callbackUrl=${callbackUrl}`, nextUrl));
});

export const config = {
    matcher: [
        '/questions/ask',
        '/questions/:id/edit',
        '/session',
        '/admin/:path*',
    ]
}
