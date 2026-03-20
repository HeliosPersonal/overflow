import {addToast} from "@heroui/toast";

export function errorToast(error: {message: string, status?: number}) {
    return addToast({
        color: 'danger',
        title: error.status || 'Error!',
        description: error.message || 'Something went wrong',
    });
}

export function successToast(message: string, title?: string) {
    return addToast({
        color: 'success',
        title: title || 'Success!',
        description: message,
    });
}

/**
 * Show an error toast for client errors (4xx), throw for server errors (500)
 * so Next.js error boundaries catch them.
 */
export function handleError(error: {message: string, status?: number}) {
    if (error.status === 500) {
        throw error;
    }
    return errorToast(error);
}

