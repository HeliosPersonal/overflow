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
    const apiUrl = process.env.API_URL;
    if (!apiUrl) return NextResponse.json({error: "Missing API_URL"}, {status: 500});

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

    const response = await fetch(`${apiUrl}${backendPath}`, {
        method,
        headers,
        ...(body !== undefined ? {body: JSON.stringify(body)} : {}),
    });

    const contentType = response.headers.get("content-type");
    const isJson = contentType?.includes("application/json");
    const data = isJson ? await response.json() : await response.text();

    const nextResponse = isJson
        ? NextResponse.json(data, {status: response.status})
        : new NextResponse(data, {status: response.status});

    // Forward any Set-Cookie from backend (guest cookie issuance)
    const setCookie = response.headers.getSetCookie?.();
    if (setCookie) {
        for (const cookie of setCookie) {
            nextResponse.headers.append("Set-Cookie", cookie);
        }
    }

    return nextResponse;
}
