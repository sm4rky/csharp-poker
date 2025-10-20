import {JSX, useEffect, useMemo, useRef, useState} from "react";
import * as Hub from "api/signalr/roomHubService";
import {TableDto} from "api/dto/TableDto";
import {PlayerDto} from "../../api/dto/PlayerDto";

interface PokerTableProps {
    baseUrl: string;
    tableCode: string;
    initialRejoinToken?: string;
}

export function DemoPokerTable({baseUrl, tableCode, initialRejoinToken}: PokerTableProps): JSX.Element {
    const [table, setTable] = useState<TableDto | null>(null);
    const [ready, setReady] = useState<any>(null);
    const [showdown, setShowdown] = useState<any>(null);
    const [defaultWin, setDefaultWin] = useState<any>(null);
    const [lastStanding, setLastStanding] = useState<any>(null);
    const [error, setError] = useState<string | null>(null);
    const [token, setToken] = useState<string | undefined>();

    const [name, setName] = useState("player name");
    const [seatIndex, setSeatIndex] = useState(0);
    const [raiseTo, setRaiseTo] = useState(0);
    const [actionLog, setActionLog] = useState<string[]>([]);
    const prevTableRef = useRef<TableDto | null>(null);

    const appendLog = (msg: string) =>
        setActionLog((prev) => [...prev, `${new Date().toLocaleTimeString()} — ${msg}`]);

    // ---- Lifecycle ----
    useEffect(() => {
        const offs = [
            Hub.on("TableState", (d) => setTable(d)),
            Hub.on("ReadyState", (d) => setReady(d)),
            Hub.on("ShowdownResult", (d) => setShowdown(d)),
            Hub.on("DefaultWinResult", (d) => setDefaultWin(d)),
            Hub.on("LastStanding", (d) => setLastStanding(d)),
            Hub.on("Error", (m) => setError(m)),
        ];
        return () => offs.forEach((off) => off());
    }, []);

    useEffect(() => {
        (async () => {
            try {
                await Hub.connect(baseUrl, tableCode);
                appendLog(`Connected to table ${tableCode}`);
                if (initialRejoinToken) {
                    try {
                        await Hub.rejoin(initialRejoinToken);
                        appendLog("Rejoined with token");
                        setToken(initialRejoinToken);
                    } catch {
                        appendLog("Rejoin failed");
                    }
                }
            } catch (e: any) {
                appendLog("Connection failed: " + e.message);
                setError(e.message);
            }
        })();
    }, [baseUrl, tableCode, initialRejoinToken]);

    // ---- Actions ----
    async function joinPlayer() {
        const tok = await Hub.joinAsPlayer(tableCode, seatIndex, name);
        setToken(tok);
        appendLog(`${name} joined seat ${seatIndex}`);
    }

    const leaveSeat = async () => {
        await Hub.leaveSeat();
        setToken(undefined);
        appendLog("Left seat");
    };
    const startNewHand = async () => {
        await Hub.startHand(tableCode);
        appendLog("Started a new hand");
    };
    const checkAction = async () => {
        await Hub.check();
        appendLog("Checked");
    };
    const callAction = async () => {
        await Hub.call();
        appendLog("Called");
    };
    const raiseAction = async () => {
        await Hub.raiseTo(raiseTo);
        appendLog(`Raised to ${raiseTo}`);
    };
    const foldAction = async () => {
        await Hub.fold();
        appendLog("Folded");
    };
    const readyNextMatch = async () => {
        await Hub.readyForNextMatch();
        appendLog("Ready for next match");
    };

    // ---- Table change logging ----
    useEffect(() => {
        const prev = prevTableRef.current;
        const curr = table;
        if (!curr || !prev) {
            prevTableRef.current = curr;
            return;
        }
        curr.players.forEach((p) => {
            const prevP = prev.players.find((x) => x.seatIndex === p.seatIndex);
            if (!prevP) return;
            if (p.lastestAction !== prevP.lastestAction && p.lastestAction)
                appendLog(`${p.name} performed: ${p.lastestAction}`);
            if (!prevP.hasFolded && p.hasFolded) appendLog(`${p.name} folded`);
            if (p.stack !== prevP.stack) {
                const diff = prevP.stack - p.stack;
                if (diff > 0) appendLog(`${p.name} bet ${diff}`);
            }
        });
        prevTableRef.current = curr;
    }, [table]);

    useEffect(() => {
        if (showdown) appendLog("Showdown! winners: " + JSON.stringify(showdown.winners));
    }, [showdown]);
    useEffect(() => {
        if (defaultWin) appendLog(`Default win: Player #${defaultWin.winner}`);
    }, [defaultWin]);
    useEffect(() => {
        if (ready)
            appendLog(
                `Ready state — seats: ${ready.readySeats?.join(", ") || "none"} (deadline ${ready.deadlineUtc})`
            );
    }, [ready]);
    useEffect(() => {
        if (lastStanding) appendLog(`Last standing: ${lastStanding.name} (${lastStanding.stack} chips)`);
    }, [lastStanding]);

    // ---- UI ----
    const fieldsetStyle = {border: "1px solid #ccc", borderRadius: 8, padding: 12, backgroundColor: "#fafafa"};
    const inputStyle = {padding: "6px 8px", border: "1px solid #bbb", borderRadius: 4, width: "100%"};
    const buttonStyle = {
        padding: "6px 10px",
        border: "1px solid #888",
        borderRadius: 4,
        backgroundColor: "#eee",
        cursor: "pointer"
    };
    const labelStyle = {display: "flex", flexDirection: "column" as const, gap: 4};

    return (
        <div style={{padding: 20, display: "grid", gap: 12, fontFamily: "sans-serif", maxWidth: 800}}>
            <h2>Table: {tableCode}</h2>
            {error && <div style={{color: "crimson"}}>Error: {error}</div>}

            {!token && (
                <fieldset style={fieldsetStyle}>
                    <legend>Join as Player</legend>
                    <div style={{display: "grid", gap: 8, maxWidth: 260}}>
                        <label style={labelStyle}>
                            <span>Name</span>
                            <input value={name} onChange={(e) => setName(e.target.value)} style={inputStyle}/>
                        </label>
                        <label style={labelStyle}>
                            <span>Seat Index (0–5)</span>
                            <input
                                type="number"
                                min={0}
                                max={5}
                                value={seatIndex}
                                onChange={(e) => setSeatIndex(parseInt(e.target.value || "0", 10))}
                                style={inputStyle}
                            />
                        </label>
                        <button style={buttonStyle} onClick={joinPlayer}>Join</button>
                    </div>
                </fieldset>
            )}

            <fieldset style={fieldsetStyle}>
                <legend>Actions</legend>
                <div style={{display: "flex", gap: 8, flexWrap: "wrap"}}>
                    <button style={buttonStyle} onClick={startNewHand}>Start Hand</button>
                    <button style={buttonStyle} onClick={checkAction}>Check</button>
                    <button style={buttonStyle} onClick={callAction}>Call</button>
                    <label style={labelStyle}>
                        <span>Raise To</span>
                        <input
                            type="number"
                            min={0}
                            value={raiseTo}
                            onChange={(e) => setRaiseTo(parseInt(e.target.value || "0", 10))}
                            style={{...inputStyle, width: 80}}
                        />
                    </label>
                    <button style={buttonStyle} onClick={raiseAction}>Raise</button>
                    <button style={buttonStyle} onClick={foldAction}>Fold</button>
                    <button style={buttonStyle} onClick={readyNextMatch}>Ready Next</button>
                    <button style={buttonStyle} onClick={leaveSeat}>Leave Seat</button>
                </div>
                {token && <div style={{marginTop: 8}}>Your token: <code>{token}</code></div>}
            </fieldset>

            <TableView table={table} fieldsetStyle={fieldsetStyle}/>
            <HumanReadableBox table={table} fieldsetStyle={fieldsetStyle}/>
            <PlayerHands table={table} token={token} fieldsetStyle={fieldsetStyle}/>
            <ActionLog fieldsetStyle={fieldsetStyle} log={actionLog}/>
            <AdvisoryBox table={table} fieldsetStyle={fieldsetStyle}/>
        </div>
    );
}

/* ---------- Subcomponents ---------- */

function TableView({table, fieldsetStyle}: { table: TableDto | null; fieldsetStyle: any }) {
    if (!table) return <div>Loading table…</div>;
    return (
        <fieldset style={fieldsetStyle}>
            <legend>Table (Debug)</legend>
            <div>Round: {table.round} | Street: {table.street} | Pot: {table.pot}</div>
            <div>Dealer: {table.dealer} | SB: {table.smallBlind} | BB: {table.bigBlind}</div>
            <div>Current Bet: {table.currentBetAmount} | Last Raise: {table.lastRaiseSize}</div>
            <div>CurrentSeatToAct: {table.currentSeatToAct} | PreviousSeatToAct: {table.previousSeatToAct}</div>
            <div style={{marginTop: 8}}>
                <b>Community:</b>{" "}
                <div style={{display: "flex", gap: 8}}>
                    {table.community?.length > 0 ? table.community.map((card: any, i: number) => (
                        <div
                            key={i}
                            style={{
                                border: "1px solid #aaa",
                                borderRadius: 6,
                                padding: "4px 8px",
                                background: "#fff",
                                fontFamily: "monospace",
                            }}
                        >
                            {card.text}
                        </div>
                    )) : <div></div>}
                </div>
            </div>
            <div style={{marginTop: 8}}>
                <b>Players:</b>
                <ul style={{marginTop: 4, paddingLeft: 20}}>
                    {table.players.map((p: PlayerDto, i) => (
                        <li key={i}>
                            #{p.seatIndex} {p.name} {p.isBot ? "(bot)" : ""} | stack={p.stack} |
                            folded={String(p.hasFolded)} | out={String(p.isOut)} |
                            committed={p.committedThisStreet}/{p.committedThisHand} | last="{p.lastestAction}"
                            {p.legalActions?.length ? <> | legal: {p.legalActions.join(", ")}</> : null}
                        </li>
                    ))}
                </ul>
            </div>
        </fieldset>
    );
}

function HumanReadableBox({table, fieldsetStyle}: { table: TableDto | null; fieldsetStyle: any }) {
    if (!table) return null;
    const current = table.players.find((p) => p.seatIndex === table.currentSeatToAct);
    const readable = [
        `Round ${table.round}, Street ${table.street}.`,
        `Pot is ${table.pot}. Dealer seat ${table.dealer}.`,
        current ? `Acting: ${current.name}.` : "No active player.",
        `Remaining: ${table.players.filter((p) => !p.hasFolded && !p.isOut).map((p) => p.name).join(", ") || "none"}.`,
    ].join(" ");
    return (
        <fieldset style={fieldsetStyle}>
            <legend>Gameplay</legend>
            <div style={{whiteSpace: "pre-wrap", lineHeight: 1.5}}>{readable}</div>
        </fieldset>
    );
}

function AdvisoryBox({table, fieldsetStyle}: { table: TableDto | null; fieldsetStyle: any }) {
    if (!table) return null;

    const hasBoard = !!table.boardAdvisory;
    const playersWithAdvisory = table.players.filter((p) => p.playerAdvisory);

    if (!hasBoard && playersWithAdvisory.length === 0)
        return (
            <fieldset style={fieldsetStyle}>
                <legend>Advisory</legend>
                <div>No advisory data yet.</div>
            </fieldset>
        );

    return (
        <fieldset style={fieldsetStyle}>
            <legend>Advisory</legend>

            {hasBoard && (
                <div style={{marginBottom: 12}}>
                    <b>Board Advisory:</b>
                    <pre
                        style={{
                            whiteSpace: "pre-wrap",
                            fontSize: "0.8em",
                            padding: 8,
                            borderRadius: 4,
                            overflowX: "auto",
                        }}
                    >
                        {JSON.stringify(table.boardAdvisory, null, 2)}
                    </pre>
                </div>
            )}

            {playersWithAdvisory.length > 0 && (
                <div>
                    <b>Player Advisory:</b>
                    <ul style={{marginTop: 4}}>
                        {playersWithAdvisory.map((p) => (
                            <li key={p.seatIndex} style={{marginBottom: 8}}>
                                <pre
                                    style={{
                                        whiteSpace: "pre-wrap",
                                        fontSize: "0.8em",
                                        padding: 6,
                                        borderRadius: 4,
                                        overflowX: "auto",
                                        marginTop: 4,
                                    }}
                                >
                                    {JSON.stringify(p.playerAdvisory, null, 2)}
                                </pre>
                            </li>
                        ))}
                    </ul>
                </div>
            )}
        </fieldset>
    );
}


function PlayerHands({table, token, fieldsetStyle}: {
    table: TableDto | null;
    token?: string;
    fieldsetStyle: any;
}) {
    if (!table || !token) return null;
    if (!table.players?.length) return null;

    return (
        <fieldset style={fieldsetStyle}>
            <legend>Players' Hands</legend>
            <div style={{display: "flex", flexDirection: "column", gap: 12}}>
                <div style={{display: "flex", gap: 8}}>
                    {table.players[0].hole.map((card: any, i: number) => (
                        <div
                            key={i}
                            style={{
                                border: "1px solid #aaa",
                                borderRadius: 6,
                                padding: "4px 8px",
                                background: "#fff",
                                fontFamily: "monospace",
                            }}
                        >
                            {card.text}
                        </div>
                    ))}
                </div>
            </div>
        </fieldset>
    );
}


function ActionLog({fieldsetStyle, log}: { fieldsetStyle: any; log: string[] }) {
    const listRef = useRef<HTMLUListElement>(null);
    useEffect(() => {
        const el = listRef.current;
        if (el) el.scrollTop = el.scrollHeight;
    }, [log]);
    return (
        <fieldset style={fieldsetStyle}>
            <legend>Action Log</legend>
            {log.length === 0 ? (
                <div>No actions yet.</div>
            ) : (
                <ul
                    ref={listRef}
                    style={{maxHeight: 200, overflowY: "auto", margin: 0, paddingLeft: 20}}
                >
                    {log.map((entry, i) => (
                        <li key={i} style={{fontFamily: "monospace", fontSize: 13}}>
                            {entry}
                        </li>
                    ))}
                </ul>
            )}
        </fieldset>
    );
}
