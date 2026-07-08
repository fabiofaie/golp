export type RatingMethod = 'Elo' | 'GameBonus';

export function ratingInfoPath(method: RatingMethod): string {
  return method === 'GameBonus' ? '/game-bonus-info' : '/elo-info';
}
