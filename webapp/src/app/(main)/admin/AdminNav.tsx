'use client';

import {Tab, Tabs} from "@heroui/tabs";
import {usePathname, useRouter} from "next/navigation";
import {Users, Tag, Shield} from "lucide-react";

const tabs = [
    {key: '/admin/users', label: 'Users', icon: Users},
    {key: '/admin/tags', label: 'Tags', icon: Tag},
];

export default function AdminNav() {
    const pathname = usePathname();
    const router = useRouter();

    const activeTab = tabs.find(t => pathname.startsWith(t.key))?.key ?? tabs[0].key;

    return (
        <div className="flex items-center gap-4 pt-4">
            <div className="flex items-center gap-2 text-foreground-800">
                <Shield className="h-5 w-5 text-primary"/>
                <h1 className="text-xl font-bold">Admin Panel</h1>
            </div>
            <Tabs
                selectedKey={activeTab}
                onSelectionChange={(key) => router.push(String(key))}
                variant="underlined"
                classNames={{tabList: "gap-4", tab: "pb-3"}}
            >
                {tabs.map(({key, label, icon: Icon}) => (
                    <Tab
                        key={key}
                        title={
                            <div className="flex items-center gap-2">
                                <Icon className="h-4 w-4"/>
                                <span>{label}</span>
                            </div>
                        }
                    />
                ))}
            </Tabs>
        </div>
    );
}

