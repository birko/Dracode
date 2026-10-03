import { defineConfig, devices } from '@playwright/test';
import { birkoPlaywrightPreset } from 'birko-web-testing/playwright';

// KoboldLair is a .NET-hosted SPA: `dotnet run` in DraCode.KoboldLair.Client serves wwwroot at
// http://localhost:57088 (Properties/launchSettings.json). The agent backend (DraCode.KoboldLair.Server)
// binds http://localhost:57087 / https://localhost:57085; the Client's ServerUrl default still reads
// ws://localhost:5000, which is stale and being reworked. The smoke is read-only and doesn't hit it.
//
// requireAuth:false → unauthenticated smoke. KoboldLair uses createAuthStore('koboldlair_auth') but its
// REST login endpoint/dev credentials aren't checked into the repo, so we sweep public routes only.
// To add authenticated routes later, see README → "Adding authenticated routes".
export default defineConfig(
  birkoPlaywrightPreset({
    baseURL: process.env.KOBOLDLAIR_BASE_URL ?? 'http://localhost:57088',
    desktopChrome: devices['Desktop Chrome'],
    requireAuth: false,
  }),
);
