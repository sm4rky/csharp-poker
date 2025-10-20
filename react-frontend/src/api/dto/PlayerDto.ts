import { CardDto } from "./CardDto";
import { PlayerAdvisoryDto } from "./PlayerAdvisoryDto";

export interface PlayerDto {
    seatIndex: number;
    name: string;
    isBot: boolean;
    hasFolded: boolean;
    hole: CardDto[];
    legalActions: string[];
    lastestAction: string;
    isOut: boolean;
    stack: number;
    committedThisStreet: number;
    committedThisHand: number;
    playerAdvisory?: PlayerAdvisoryDto | null;
}
