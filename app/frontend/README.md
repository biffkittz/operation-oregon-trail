# STARWAGON frontend

Vue 3 + TypeScript + Vite + Tailwind CSS v4 single-page app for the STARWAGON game. In production it is built into
`dist/`, copied into the backend's `wwwroot` and served by ASP.NET Core from the same origin as the API.

```bash
npm ci
npm run dev        # http://localhost:5173 (needs the backend on :5069: see ../../docs/development-setup.md)
npm run check      # type-check, lint, unit tests
npm run build      # production build into dist/
npm run test:e2e   # Playwright smoke tests against a running site
```

Conventions, structure and gotchas: [CLAUDE.md](CLAUDE.md). Whole-project docs: [../../README.md](../../README.md).
