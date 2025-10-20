import { PlayerDto } from "./PlayerDto";
import { CardDto } from "./CardDto";
import { BoardAdvisoryDto } from "./BoardAdvisoryDto";

export interface TableDto {
    tableCode: string;
    round: number;
    street: string;
    players: PlayerDto[];
    community: CardDto[];
    dealer: number;
    smallBlind: number;
    bigBlind: number;
    currentSeatToAct: number;
    previousSeatToAct: number;
    closingSeat: number;
    pot: number;
    smallBlindAmount: number;
    bigBlindAmount: number;
    currentBetAmount: number;
    lastRaiseSize: number;
    boardAdvisory?: BoardAdvisoryDto | null;
}
