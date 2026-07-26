# IronWord WebGL build

Output folder: `webGL/` (Unity WebGL player).

## Size note

The current uncompressed build is ~430+ MB (`webGL.data` alone is ~370 MB).

| Host | Practical limit | Fits this build? |
| --- | --- | --- |
| Vercel Hobby | 100 MB upload | No |
| Vercel Pro | 1 GB upload | Yes (barely) |
| itch.io | Large games OK | Yes (recommended free) |

## Shrink before deploy (strongly recommended)

In Unity → **Player Settings → Publishing Settings**:

1. **Compression Format** → `Brotli` (or `Gzip`)
2. Rebuild WebGL into `build/webGL`

Then redeploy. Compressed builds are usually much smaller and load faster.

## Deploy to Vercel (after size fits)

From this repo root:

```bash
npx vercel --cwd build/webGL --prod
```

Or in the Vercel dashboard: set **Root Directory** to `build/webGL`.

`vercel.json` in `webGL/` sets the MIME / encoding headers Unity needs.
