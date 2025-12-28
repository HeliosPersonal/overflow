// TODO: Remove Cloudinary - commented out for now
// This API route will be deleted in the future

/*
import {cloudinary} from "@/lib/cloudinary";
import {cloudinaryConfig} from "@/lib/config";

export async function POST(request: Request) {
    const body = (await request.json()) as {paramsToSign: Record<string, string>}
    const {paramsToSign} = body;
    
    const signature = cloudinary.v2.utils.api_sign_request(paramsToSign, 
        cloudinaryConfig.apiSecret);
    
    return Response.json({signature});
}
*/

export async function POST(_request: Request) {
    return Response.json({ error: 'Cloudinary integration disabled' }, { status: 503 });
}
