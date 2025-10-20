import * as signalR from "@microsoft/signalr";
import {TableDto} from "api/dto/TableDto";
import {ReadyInfoDto} from "api/dto/ReadyInfoDto";
import {DefaultWinResultDto} from "api/dto/DefaultWinResultDto";
import {ShowdownResultDto} from "api/dto/ShowdownResultDto";
import {PlayerDto} from "api/dto/PlayerDto";

type ServerEvents = {
    TableState: TableDto;
    ReadyState: ReadyInfoDto;
    DefaultWinResult: DefaultWinResultDto;
    ShowdownResult: ShowdownResultDto;
    LastStanding: PlayerDto;
    Error: string;
};
type Listener<K extends keyof ServerEvents> = (p: ServerEvents[K]) => void;

// ---- Module-level singleton state ----
// (This module represents one persistent SignalR connection across all components)
let conn: signalR.HubConnection | null = null;
let startPromise: Promise<void> | null = null;
let currentOrigin = "";
let currentTableCode: string | undefined;
let token: string | undefined;

// ---- In-memory event listeners registry ----
const listeners: { [K in keyof ServerEvents]: Set<Listener<K>> } = {
    TableState: new Set(),
    ReadyState: new Set(),
    DefaultWinResult: new Set(),
    ShowdownResult: new Set(),
    LastStanding: new Set(),
    Error: new Set(),
};

// Helper to emit typed events to all active listeners
function emit<K extends keyof ServerEvents>(evt: K, payload: ServerEvents[K]) {
    for (const h of listeners[evt]) h(payload);
}

// Wire up SignalR → local emit() once per connection
function wire(c: signalR.HubConnection) {
    c.on("TableState", (d: TableDto) => emit("TableState", d));
    c.on("ReadyState", (d: ReadyInfoDto) => emit("ReadyState", d));
    c.on("DefaultWinResult", (d: DefaultWinResultDto) => emit("DefaultWinResult", d));
    c.on("ShowdownResult", (d: ShowdownResultDto) => emit("ShowdownResult", d));
    c.on("LastStanding", (d: PlayerDto) => emit("LastStanding", d));
    c.on("Error", (m: string) => emit("Error", m));
}

// Build a new HubConnection (dev: WS only; prod: full negotiate)
function build(origin: string) {
    conn = new signalR.HubConnectionBuilder()
        .withUrl(`${origin}/roomhub`, {withCredentials: true})
        .withAutomaticReconnect()
        .build();
    wire(conn);
}

// ---- Public API ----

// Subscribe to an event; returns an unsubscribe function
export function on<K extends keyof ServerEvents>(evt: K, h: Listener<K>) {
    listeners[evt].add(h);
    return () => listeners[evt].delete(h);
}

// Establish or reuse the shared connection
export async function connect(baseUrl: string, tableCode: string) {
    const origin = new URL(baseUrl, window.location.href).origin;
    if (!conn) {
        build(origin);
        currentOrigin = origin;
    }
    if (origin !== currentOrigin)
        throw new Error("Hub already bound to a different origin.");

    // Prevent overlapping negotiation (React StrictMode causes double start)
    if (!startPromise) {
        startPromise = conn!.start()
            .catch((e) => {
                // Ignore the known “stopped during negotiation” dev race
                if (String(e?.message || e).toLowerCase().includes("negotiation")) return;
                throw e;
            })
            .finally(() => {
                startPromise = null;
            });
    }
    await startPromise;

    // Only join if table changed
    if (currentTableCode !== tableCode) {
        currentTableCode = tableCode;
        await conn!.invoke("JoinRoom", tableCode);
        if (token) {
            try {
                await conn!.invoke("Rejoin", token);
            } catch {
            }
        }
    }
}

export async function disconnect() {
    if (!conn) return;
    try {
        await conn.stop();
    } catch {
    }
    conn = null;
    currentTableCode = undefined;
    token = undefined;
}

// ---- Game action wrappers ----
export async function joinAsPlayer(tableCode: string, seat: number, name: string) {
    if (!conn) throw new Error("Connect first");
    const t = await conn.invoke<string>("JoinAsPlayer", tableCode, seat, name);
    token = t;
    return t;
}

export async function rejoin(t: string) {
    if (!conn) throw new Error("Connect first");
    await conn.invoke("Rejoin", t);
    token = t;
}

export async function leaveSeat() {
    if (!conn || !token) return;
    await conn.invoke("LeaveSeat", token);
    token = undefined;
}

export async function startHand(tableCode: string) {
    if (!conn) throw new Error("Connect first");
    await conn.invoke("StartHand", tableCode);
}

export async function check() {
    if (!conn || !token) throw new Error("Join first");
    await conn.invoke("Check", token);
}

export async function call() {
    if (!conn || !token) throw new Error("Join first");
    await conn.invoke("Call", token);
}

export async function raiseTo(amount: number) {
    if (!conn || !token) throw new Error("Join first");
    await conn.invoke("Raise", token, amount);
}

export async function fold() {
    if (!conn || !token) throw new Error("Join first");
    await conn.invoke("Fold", token);
}

export async function readyForNextMatch() {
    if (!conn || !token) throw new Error("Join first");
    await conn.invoke("ReadyForNextMatch", token);
}

// Expose a few readonly state fields for inspection
export const hubState = {
    get token() {
        return token;
    },
    get tableCode() {
        return currentTableCode;
    },
};