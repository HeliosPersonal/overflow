import cloudinary from "cloudinary";
import {cloudinaryConfig} from "@/lib/config";

let isConfigured = false;

/**
 * Lazy initialization of Cloudinary configuration
 * Ensures environment variables are loaded before configuration
 */
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

// Create a proxy object that ensures configuration before any operation
const cloudinaryProxy = new Proxy(cloudinary, {
    get(target, prop) {
        ensureCloudinaryConfigured();
        return target[prop as keyof typeof cloudinary];
    }
});

export { cloudinaryProxy as cloudinary };
