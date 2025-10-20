export interface CreateTableResponse {
    tableCode: string;
}

export async function createTable(baseUrl: string, playerCount: number): Promise<CreateTableResponse> {
    const b = baseUrl.replace(/\/+$/, "");
    const url = new URL("/api/table/create", b);
    url.searchParams.set("playerCount", String(playerCount));

    const res = await fetch(url.toString(), {method: "POST"});
    if (!res.ok) throw new Error(await safeText(res));
    return (await res.json()) as CreateTableResponse;
}

async function safeText(res: Response) {
    try {
        return await res.text();
    } catch {
        return `${res.status} ${res.statusText}`;
    }
}
