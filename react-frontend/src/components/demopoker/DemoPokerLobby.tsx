import { JSX, useState } from "react";
import { createTable } from "api/rest/pokerApi";
import { DemoPokerTable } from "./DemoPokerTable";
import { API_URL } from "utils/const";

interface PokerLobbyProps {}

export function DemoPokerLobby(_: PokerLobbyProps): JSX.Element {
    const [baseUrl, setBaseUrl] = useState<string>(API_URL);
    const [playerCount, setPlayerCount] = useState<number>(4);
    const [tableCode, setTableCode] = useState<string>("");
    const [connectNow, setConnectNow] = useState(false);
    const [rejoinToken, setRejoinToken] = useState<string>("");
    const [err, setErr] = useState<string | null>(null);

    const onCreate = async () => {
        setErr(null);
        try {
            const { tableCode } = await createTable(baseUrl, playerCount);
            setTableCode(tableCode);
        } catch (e: any) {
            setErr(e?.message ?? String(e));
        }
    };

    if (connectNow && tableCode) {
        return (
            <DemoPokerTable
                baseUrl={baseUrl}
                tableCode={tableCode}
                initialRejoinToken={rejoinToken || undefined}
            />
        );
    }

    const fieldStyle = {
        display: "flex",
        flexDirection: "column" as any,
        gap: 4,
        marginBottom: 8,
    };

    const fieldsetStyle = {
        border: "1px solid #ccc",
        borderRadius: 8,
        padding: 12,
        backgroundColor: "#fafafa",
    };

    const inputStyle = {
        width: "100%",
        padding: "6px 8px",
        border: "1px solid #bbb",
        borderRadius: 4,
    };

    const buttonStyle = {
        padding: "6px 10px",
        border: "1px solid #888",
        borderRadius: 4,
        backgroundColor: "#eee",
        cursor: "pointer",
    };

    return (
        <div style={{ padding: 20, fontFamily: "sans-serif" }}>
            <h1 style={{ marginBottom: 16 }}>Poker Lobby</h1>
            <div style={{ display: "grid", gap: 12, maxWidth: 520 }}>
                <label style={fieldStyle}>
                    <span>API Base URL</span>
                    <input
                        style={inputStyle}
                        value={baseUrl}
                        onChange={(e) => setBaseUrl(e.target.value)}
                        placeholder={API_URL}
                    />
                </label>

                <fieldset style={fieldsetStyle}>
                    <legend>Create Room</legend>
                    <label style={fieldStyle}>
                        <span>Player Count</span>
                        <input
                            type="number"
                            min={2}
                            max={6}
                            value={playerCount}
                            onChange={(e) =>
                                setPlayerCount(parseInt(e.target.value || "4", 10))
                            }
                            style={inputStyle}
                        />
                    </label>
                    <button style={buttonStyle} onClick={onCreate}>
                        Create
                    </button>
                    <div style={{ marginTop: 6 }}>
                        Created Table Code: <b>{tableCode || "(none)"}</b>
                    </div>
                </fieldset>

                <fieldset style={fieldsetStyle}>
                    <legend>Join / Rejoin</legend>
                    <label style={fieldStyle}>
                        <span>Table Code</span>
                        <input
                            value={tableCode}
                            onChange={(e) => setTableCode(e.target.value)}
                            placeholder="enter table code"
                            style={inputStyle}
                        />
                    </label>
                    <label style={fieldStyle}>
                        <span>Rejoin Token (optional)</span>
                        <input
                            value={rejoinToken}
                            onChange={(e) => setRejoinToken(e.target.value)}
                            placeholder="paste a previous token"
                            style={inputStyle}
                        />
                    </label>
                    <button
                        style={{
                            ...buttonStyle,
                            opacity: !tableCode ? 0.6 : 1,
                        }}
                        disabled={!tableCode}
                        onClick={() => setConnectNow(true)}
                    >
                        Connect
                    </button>
                </fieldset>

                {err && <div style={{ color: "crimson" }}>Error: {err}</div>}
            </div>
        </div>
    );
}
