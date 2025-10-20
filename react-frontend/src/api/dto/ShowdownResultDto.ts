import { HandResultDto } from "./HandResultDto";

export interface ShowdownResultDto {
    winners: number[];
    hands: HandResultDto[];
}
