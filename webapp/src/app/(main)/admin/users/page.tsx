import {getAdminUsers} from "@/lib/actions/admin-actions";
import UsersTable from "./UsersTable";

type Props = {
    searchParams: Promise<{ search?: string; page?: string; pageSize?: string }>;
};

export default async function AdminUsersPage({searchParams}: Props) {
    const params = await searchParams;
    const {data, error} = await getAdminUsers({
        search: params.search,
        page: params.page ? Number(params.page) : undefined,
        pageSize: params.pageSize ? Number(params.pageSize) : undefined,
    });

    if (error) {
        return <p className="text-sm text-foreground-400">{error.message}</p>;
    }

    return <UsersTable data={data ?? {items: [], totalCount: 0, page: 1, pageSize: 5}}/>;
}
