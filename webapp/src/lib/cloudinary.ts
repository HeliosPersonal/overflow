// TODO: Remove Cloudinary integration - commented out for now
// This file will be deleted in the future

/*
import cloudinary from "cloudinary";
import {cloudinaryConfig} from "@/lib/config";

let isConfigured = false;

function ensureCloudinaryConfigured() {
    if (isConfigured) return;

    console.log('🌥️  [Cloudinary] Configuring SDK...', {
        hasCloudName: !!process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME,
        hasApiKey: !!process.env.CLOUDINARY_API_KEY,
        hasApiSecret: !!process.env.CLOUDINARY_API_SECRET,
    });

    cloudinary.v2.config({
        cloud_name: cloudinaryConfig.cloudName,
        api_key: cloudinaryConfig.apiKey,
        api_secret: cloudinaryConfig.apiSecret,
    });

    isConfigured = true;
    console.log('✅ [Cloudinary] SDK configured');
}

const cloudinaryProxy = new Proxy(cloudinary, {
    get(target, prop) {
        ensureCloudinaryConfigured();
        return target[prop as keyof typeof cloudinary];
    }
});

export { cloudinaryProxy as cloudinary };
*/

// Placeholder export to prevent import errors
export const cloudinary = null;
