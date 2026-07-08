import { ratingInfoPath } from './rating-method.util';

describe('ratingInfoPath', () => {
  it('returns /elo-info for Elo', () => {
    expect(ratingInfoPath('Elo')).toBe('/elo-info');
  });

  it('returns /game-bonus-info for GameBonus', () => {
    expect(ratingInfoPath('GameBonus')).toBe('/game-bonus-info');
  });
});
