import {auth} from "@/auth";
import {redirect} from "next/navigation";
import React from "react";
import AdminNav from "./AdminNav";

export default async function AdminLayout({children}: { children: React.ReactNode }) {
    const session = await auth();
    if (!session?.user?.roles?.includes('admin')) redirect('/questions');

    return (
        <div className="min-h-full bg-content1 w-full">
            <div className="border-b border-content3/50 px-3 sm:px-6">
                <AdminNav/>
            </div>
            <div className="px-3 sm:px-6 py-6">
                {children}
            </div>
        </div>
    );
}

