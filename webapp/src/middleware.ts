import {auth} from "@/auth";
import {NextResponse} from "next/server";

export default auth((req) => {
    const { nextUrl, auth: authData } = req;
    
    console.log('[Middleware] ========== MIDDLEWARE START ==========');
    console.log('[Middleware] Timestamp:', new Date().toISOString());
    console.log('[Middleware] Path:', nextUrl.pathname);
    console.log('[Middleware] Search:', nextUrl.search);
    console.log('[Middleware] Has auth:', !!authData);
    
    if (authData) {
        console.log('[Middleware] User authenticated:', {
            user: authData.user?.email || 'N/A'
        });
        console.log('[Middleware] ========== MIDDLEWARE END (AUTHENTICATED) ==========');
        return NextResponse.next();
    }
    
    const callbackUrl = encodeURIComponent(nextUrl.pathname + nextUrl.search);
    console.log('[Middleware] User NOT authenticated, redirecting to auth-gate');
    console.log('[Middleware] Callback URL:', callbackUrl);
    console.log('[Middleware] ========== MIDDLEWARE END (REDIRECT) ==========');
    
    return NextResponse.redirect(new URL(`/auth-gate?callbackUrl=${callbackUrl}`, nextUrl));
});

export const config = {
    matcher: [
        '/questions/ask',
        '/questions/:id/edit',
        '/session'
    ]
}

