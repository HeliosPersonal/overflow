import {NextRequest} from "next/server";
import {proxyEstimation} from "@/app/api/estimation/proxy";

export async function DELETE(req: NextRequest) {
    return proxyEstimation(req, "/estimation/profile-cache", "DELETE");
}

