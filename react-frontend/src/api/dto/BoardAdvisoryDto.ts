export interface BoardAdvisoryDto {
    street: string;
    texture: string;
    paired: boolean;
    monotone: boolean;
    straightThreatScore: number;
    flushThreatScore: number;
    tripsPossibleOnBoard: boolean;
}