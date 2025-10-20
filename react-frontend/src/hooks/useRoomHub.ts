import {useCallback, useEffect, useMemo, useRef, useState} from "react";
import {RoomHub} from "../api/signalr/roomHub";
import {TableDto} from "../api/dto/TableDto";
import {ReadyInfoDto} from "../api/dto/ReadyInfoDto";
import {DefaultWinResultDto} from "../api/dto/DefaultWinResultDto";
import {ShowdownResultDto} from "../api/dto/ShowdownResultDto";
import {PlayerDto} from "../api/dto/PlayerDto";

export function useRoomHub(baseUrl: string) {
    // Keep the RoomHub instance stable across renders
    const hubRef = useRef<RoomHub | null>(null);

    // Core connection state
    const [isConnected, setIsConnected] = useState(false);
    const [tableCode, setTableCode] = useState<string | undefined>(undefined);
    const [token, setToken] = useState<string | undefined>(undefined);

    // Server-driven data
    const [table, setTable] = useState<TableDto | null>(null);
    const [ready, setReady] = useState<ReadyInfoDto | null>(null);
    const [showdown, setShowdown] = useState<ShowdownResultDto | null>(null);
    const [defaultWin, setDefaultWin] = useState<DefaultWinResultDto | null>(null);
    const [lastStanding, setLastStanding] = useState<PlayerDto | null>(null);
    const [error, setError] = useState<string | null>(null);

    // Initialize hub on mount / cleanup on unmount
    useEffect(() => {
        hubRef.current = new RoomHub(baseUrl);
        return () => {
            hubRef.current?.disconnect().catch(() => void 0);
            hubRef.current = null;
        };
    }, [baseUrl]);

    // Register server event listeners
    useEffect(() => {
        const hub = hubRef.current;
        if (!hub) return;

        // Table state updates (main game state)
        const offTable = hub.on("TableState", (dto) => {
            setTable(dto);
            // Reset ephemeral state when new hand starts
            if (dto.street?.toLowerCase() === "preflop") {
                setShowdown(null);
                setDefaultWin(null);
                setReady(null);
            }
        });

        // Match ready state updates
        const offReady = hub.on("ReadyState", (dto) => setReady(dto));

        // Default-win and showdown events
        const offDefaultWin = hub.on("DefaultWinResult", (dto) => {
            setDefaultWin(dto);
            setShowdown(null);
        });
        const offShowdown = hub.on("ShowdownResult", (dto) => {
            setShowdown(dto);
            setDefaultWin(null);
        });

        // Winner announcement
        const offLastStand = hub.on("LastStanding", (dto) => {
            setLastStanding(dto);
            setShowdown(null);
            setDefaultWin(null);
            setReady(null);
        });

        // Error event
        const offErr = hub.on("Error", (msg) => setError(msg));

        // Cleanup handlers on unmount
        return () => {
            offTable();
            offReady();
            offDefaultWin();
            offShowdown();
            offLastStand();
            offErr();
        };
    }, [baseUrl]);

    /* ===========================
       Hub actions (client → server)
       =========================== */

    const connect = useCallback(async (code: string) => {
        const hub = hubRef.current!;
        await hub.connect(code);
        setIsConnected(true);
        setTableCode(code);
    }, []);

    const disconnect = useCallback(async () => {
        const hub = hubRef.current;
        if (!hub) return;
        await hub.disconnect();
        setIsConnected(false);
        setTableCode(undefined);
    }, []);

    const joinAsPlayer = useCallback(async (code: string, seatIndex: number, name: string) => {
        const hub = hubRef.current!;
        const t = await hub.joinAsPlayer(code, seatIndex, name);
        setToken(t);
        return t;
    }, []);

    const rejoin = useCallback(async (t: string) => {
        const hub = hubRef.current!;
        await hub.rejoin(t);
        setToken(t);
    }, []);

    const leaveSeat = useCallback(async () => {
        const hub = hubRef.current!;
        await hub.leaveSeat();
        setToken(undefined);
    }, []);

    const startHand = useCallback(async (code: string) => {
        const hub = hubRef.current!;
        await hub.startHand(code);
    }, []);

    // Player in-hand actions
    const check = useCallback(async () => hubRef.current!.check(), []);
    const call = useCallback(async () => hubRef.current!.call(), []);
    const raise = useCallback(async (amount: number) => hubRef.current!.raise(amount), []);
    const fold = useCallback(async () => hubRef.current!.fold(), []);
    const readyForNextMatch = useCallback(async () => hubRef.current!.readyForNextMatch(), []);

    return useMemo(
        () => ({
            // State
            isConnected,
            tableCode,
            token,
            table,
            ready,
            showdown,
            defaultWin,
            lastStanding,
            error,

            // Actions
            connect,
            disconnect,
            joinAsPlayer,
            rejoin,
            leaveSeat,
            startHand,
            check,
            call,
            raise,
            fold,
            readyForNextMatch,
        }),
        [
            isConnected,
            tableCode,
            token,
            table,
            ready,
            showdown,
            defaultWin,
            lastStanding,
            error,
            connect,
            disconnect,
            joinAsPlayer,
            rejoin,
            leaveSeat,
            startHand,
            check,
            call,
            raise,
            fold,
            readyForNextMatch,
        ]
    );
}
