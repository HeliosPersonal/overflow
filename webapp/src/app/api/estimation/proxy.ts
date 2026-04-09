import {auth} from "@/auth";
import {NextRequest, NextResponse} from "next/server";

const GUEST_COOKIE = "overflow_guest_id";

/**
 * Shared proxy helper for all estimation API route handlers.
 * Forwards requests to the backend Estimation Service, attaching the Bearer token
 * for authenticated users or the guest cookie for guest participants.
 */
export async function proxyEstimation(
    req: NextRequest,
    backendPath: string,
    method: string,
    body?: unknown
): Promise<NextResponse> {
    // Use a dedicated internal URL for estimation-svc so the server-side proxy calls
    // the service directly, bypassing the public ingress entirely.
    // Falls back to API_URL for local dev (Aspire gateway handles routing there).
    const estimationUrl = process.env.ESTIMATION_SERVICE_URL ?? process.env.API_URL;
    if (!estimationUrl) return NextResponse.json({error: "Missing ESTIMATION_SERVICE_URL"}, {status: 500});

    const session = await auth();
    const headers: HeadersInit = {"Content-Type": "application/json"};

    if (session?.accessToken) {
        headers["Authorization"] = `Bearer ${session.accessToken}`;
    }

    // Forward guest cookie to backend
    const guestId = req.cookies.get(GUEST_COOKIE)?.value;
    if (guestId) {
        headers["Cookie"] = `${GUEST_COOKIE}=${guestId}`;
    }

    const response = await fetch(`${estimationUrl}${backendPath}`, {
        method,
        headers,
        ...(body !== undefined ? {body: JSON.stringify(body)} : {}),
    });

    // 204 No Content — must not have a body (NextResponse.json crashes with status 204)
    if (response.status === 204) {
        return new NextResponse(null, {status: 204});
    }

    const contentType = response.headers.get("content-type");
    const isJson = contentType?.includes("application/json");

    // Always use NextResponse.json to avoid new NextResponse(string) which
    // triggers a TransformStream incompatibility in Node.js (Web Streams API bug).
    let data: unknown;
    if (isJson) {
        data = await response.json();
    } else {
        const text = await response.text();
        data = text ? {message: text} : null;
    }

    const nextResponse = NextResponse.json(data, {status: response.status});

    // Prevent browser / CDN caching — estimation data is dynamic and must always be fresh.
    nextResponse.headers.set("Cache-Control", "no-store");

    // Forward any Set-Cookie from backend (guest cookie issuance)
    const setCookieHeader = response.headers.get("set-cookie");
    if (setCookieHeader) {
        nextResponse.headers.append("Set-Cookie", setCookieHeader);
    }

    return nextResponse;
}
