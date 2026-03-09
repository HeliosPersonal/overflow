import {NextRequest} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function POST(req: NextRequest, {params}: {params: Promise<{code: string}>}) {
    const {code} = await params;
    const body = await req.json();
    return proxyEstimation(req, `/estimation/rooms/${code}/votes`, "POST", body);
}

export async function DELETE(req: NextRequest, {params}: {params: Promise<{code: string}>}) {
    const {code} = await params;
    return proxyEstimation(req, `/estimation/rooms/${code}/votes/me`, "DELETE");
}

