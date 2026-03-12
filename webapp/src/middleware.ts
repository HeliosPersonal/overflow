import {auth} from "@/auth";
import {NextResponse} from "next/server";

export default auth((req) => {
    const { nextUrl, auth: authData } = req;
    
    if (authData) {
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
        '/tags/manage',
    ]
}
