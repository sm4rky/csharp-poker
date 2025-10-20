import * as signalR from "@microsoft/signalr";
import {TableDto} from "api/dto/TableDto";
import {ReadyInfoDto} from "api/dto/ReadyInfoDto";
import {DefaultWinResultDto} from "api/dto/DefaultWinResultDto";
import {ShowdownResultDto} from "api/dto/ShowdownResultDto";
import {PlayerDto} from "api/dto/PlayerDto";

export type ServerEvents = {
    TableState: TableDto;
    ReadyState: ReadyInfoDto;
    DefaultWinResult: DefaultWinResultDto;
    ShowdownResult: ShowdownResultDto;
    LastStanding: PlayerDto;
    Error: string;
};

type Listener<K extends keyof ServerEvents> = (payload: ServerEvents[K]) => void;

export class RoomHub {
    private baseUrl: string;
    private connection: signalR.HubConnection | null = null;
    private tableCode?: string;
    private token?: string;

    private listeners: { [K in keyof ServerEvents]: Set<Listener<K>> } = {
        TableState: new Set(),
        ReadyState: new Set(),
        DefaultWinResult: new Set(),
        ShowdownResult: new Set(),
        LastStanding: new Set(),
        Error: new Set(),
    };

    constructor(baseUrl: string) {
        this.baseUrl = baseUrl.replace(/\/+$/, "");
    }

    get isConnected() {
        return this.connection?.state === signalR.HubConnectionState.Connected;
    }

    on<K extends keyof ServerEvents>(event: K, handler: Listener<K>) {
        this.listeners[event].add(handler);
        return () => this.off(event, handler);
    }

    off<K extends keyof ServerEvents>(event: K, handler: Listener<K>) {
        this.listeners[event].delete(handler);
    }

    async connect(tableCode: string) {
        if (this.isConnected && this.tableCode === tableCode) return;
        if (this.connection) {
            try {
                await this.connection.stop();
            } catch {
            }
            this.connection = null;
        }

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`${this.baseUrl}/roomhub`)
            .withAutomaticReconnect()
            .build();

        this.wireServerEvents(this.connection);
        await this.connection.start();

        this.tableCode = tableCode;
        await this.connection.invoke("JoinRoom", tableCode);
        if (this.token) {
            try {
                await this.connection.invoke("Rejoin", this.token);
            } catch {
            }
        }
    }

    async disconnect() {
        if (!this.connection) return;
        try {
            await this.connection.stop();
        } catch {
        }
        this.connection = null;
        this.tableCode = undefined;
    }

    async joinAsPlayer(tableCode: string, seatIndex: number, name: string): Promise<string> {
        await this.ensureConnected(tableCode);
        const token = await this.connection!.invoke<string>("JoinAsPlayer", tableCode, seatIndex, name);
        this.token = token;
        return token;
    }

    async rejoin(token: string) {
        if (!this.tableCode) throw new Error("No table to rejoin.");
        await this.ensureConnected(this.tableCode);
        await this.connection!.invoke("Rejoin", token);
        this.token = token;
    }

    async leaveSeat() {
        if (!this.connection || !this.token) return;
        await this.connection.invoke("LeaveSeat", this.token);
        this.token = undefined;
    }

    async startHand(tableCode: string) {
        await this.ensureConnected(tableCode);
        await this.connection!.invoke("StartHand", tableCode);
    }

    async check() {
        await this.ensurePlayer();
        await this.connection!.invoke("Check", this.token!);
    }

    async call() {
        await this.ensurePlayer();
        await this.connection!.invoke("Call", this.token!);
    }

    async raise(amount: number) {
        await this.ensurePlayer();
        await this.connection!.invoke("Raise", this.token!, amount);
    }

    async fold() {
        await this.ensurePlayer();
        await this.connection!.invoke("Fold", this.token!);
    }

    async readyForNextMatch() {
        await this.ensurePlayer();
        await this.connection!.invoke("ReadyForNextMatch", this.token!);
    }

    /* ==== internal helpers ==== */
    private async ensureConnected(tableCode: string) {
        if (!this.isConnected || this.tableCode !== tableCode) {
            await this.connect(tableCode);
        }
    }

    private async ensurePlayer() {
        if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected)
            throw new Error("Hub not connected.");
        if (!this.token)
            throw new Error("Player token missing. Call joinAsPlayer() or rejoin() first.");
    }

    private wireServerEvents(conn: signalR.HubConnection) {
        conn.on("TableState", (dto: TableDto) => this.emit("TableState", dto));
        conn.on("ReadyState", (dto: ReadyInfoDto) => this.emit("ReadyState", dto));
        conn.on("DefaultWinResult", (dto: DefaultWinResultDto) => this.emit("DefaultWinResult", dto));
        conn.on("ShowdownResult", (dto: ShowdownResultDto) => this.emit("ShowdownResult", dto));
        conn.on("LastStanding", (dto: PlayerDto) => this.emit("LastStanding", dto));
        conn.on("Error", (msg: string) => this.emit("Error", msg));
    }

    private emit<K extends keyof ServerEvents>(event: K, payload: ServerEvents[K]) {
        for (const h of this.listeners[event]) h(payload);
    }
}
