import {CardDto} from "./CardDto";

export interface PlayerAdvisoryDto {
    street: string;
    currentHandRank: string;
    bestFiveCards: CardDto[]
    strengthPercentile: number;
    flushDraw: string;
    straightDraw: string;
    overcards: string;
}