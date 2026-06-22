import { getInstallGuide } from './pwa-install-steps';

describe('getInstallGuide', () => {
  it('restituisce contenuto non vuoto per ios/safari', () => {
    const guide = getInstallGuide('ios', 'safari');
    expect(guide.steps.length).toBeGreaterThan(0);
    expect(guide.hasNativePrompt).toBe(false);
  });

  it('restituisce contenuto non vuoto per android/chrome con native prompt', () => {
    const guide = getInstallGuide('android', 'chrome');
    expect(guide.steps.length).toBeGreaterThan(0);
    expect(guide.hasNativePrompt).toBe(true);
  });

  it('restituisce contenuto non vuoto per android/samsung', () => {
    const guide = getInstallGuide('android', 'samsung');
    expect(guide.steps.length).toBeGreaterThan(0);
  });

  it('restituisce contenuto non vuoto per android/firefox', () => {
    const guide = getInstallGuide('android', 'firefox');
    expect(guide.steps.length).toBeGreaterThan(0);
  });

  it('restituisce il fallback per combinazioni non mappate', () => {
    const guide = getInstallGuide('other', 'other');
    expect(guide.steps.length).toBeGreaterThan(0);
    expect(guide.hasNativePrompt).toBe(false);
  });

  it('restituisce il fallback per android/other (browser non riconosciuto)', () => {
    const guide = getInstallGuide('android', 'other');
    expect(guide.steps.length).toBeGreaterThan(0);
  });
});
