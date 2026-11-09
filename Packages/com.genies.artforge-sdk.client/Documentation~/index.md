# Genies SDK ArtForge

`com.genies.sdk.artforge` is an Editor-only Unity package for generating 3D assets with the Genies backend.

## Open the Hub

- Menu: `Tools > Genies > Art Forge Hub`
- Shortcut: `Cmd+Ctrl+G` (macOS) / `Ctrl+G` (Windows)

## Quick Start

1. Open `Settings` in the Hub.
2. In `Backend Settings` click `Login`.
3. Optionally configure:
   - `Project Folder` (default: `Assets/Genies/ArtForge`)
   - `Enable Logs`
4. Go to `Model Generator`:
   - Choose an asset category/type.
   - Enter a description (when required by the selected category).
   - Enter a unique asset name.
   - Optionally attach reference images.
   - Set quality options and click `Generate Model`.
5. Track progress in the right-side queue panel. Approve preview images when prompted.
6. Use `History` to review completed/failed jobs and `Model Editor` to create edits from existing generated assets.

## Input Rules

- Asset name: `2-30` characters, letters/numbers/spaces/hyphens/underscores.
- Description: `5-2000` characters when required by feature availability.
- Reference images: up to `4` images, `PNG/JPG/JPEG`, max `10MB` each (when reference images are enabled).

## Output

Generated files are saved under `Assets/Genies/ArtForge/` (or your configured project folder), with per-asset subfolders such as:

- `Models/`
- `Textures/`
- `Materials/`
- `Prefabs/`
- `RefImages/` and `RefImages/History/`
- `Source/` (for source model files such as GLB)

## Troubleshooting

- `Not Authenticated` or disconnected: reconnect from `Settings > Backend Settings`.
- Duplicate name errors: use a new asset name or remove conflicting existing asset folders/jobs.
- If generation options are missing, feature availability for your environment may be restricting that UI.
