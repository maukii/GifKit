# GIF Kit

Record seamless looping GIFs and stills in play mode and generate tiling backgrounds for itch.io and other store pages

Everything runs in the editor and nothing ends up in builds

![VIGIL itch.io banner](Documentation~/vigil-banner.gif)

*VIGIL's itch.io banner: real snow particles looped, candle flicker blended, title as an overlay (960 x 320, 96 frames, 222 KB)*

## Install

1. Open **Window -> Package Manager**
2. Click **+** and choose **Install package from git URL...** (**Add package from git URL...** in older Unity versions)
3. Paste `https://github.com/maukii/GifKit.git`

To use a local copy instead, choose **Install package from disk...** and pick `package.json`

Open the tool from **Window -> GIF Kit**

## Capture

Enter play mode, set up the moment you want and press **Record GIF** or **Take still**

Files are saved to `Captures/` in the project folder (outside `Assets`) unless you pick another folder

- **Source** - **Camera** renders one camera off screen at the exact size you ask for, **Game View** grabs the Game view as it is with UI included
- **Camera pose** - **Free** leaves the camera alone, **Hold** freezes it where it is (no head bob or drift), **Scene View** uses the Scene view's angle so you can frame the shot there
- **Size** - presets for itch.io banners, covers and screenshots, or any size you type in
- **Pixel scale** - renders at 1/N size and scales up with hard pixels, pick the scale that matches your game's pixel resolution
- **Frames and FPS** - the loop length, game time moves exactly one frame per capture so recordings stay smooth even when the editor is slow
- **Loop particles** - makes every playing particle system loop perfectly
- **Blend frames** - dissolves the last frames into the first with an ordered dither, hiding the jump in anything that doesn't loop on its own like light flicker, shader noise or animation
- **Palette** - **Auto** keeps every colour exact when there are 255 or fewer and reduces to 255 otherwise, **Fixed** maps everything to the colours you list
- **Overlay** - a texture stamped on every frame such as a logo or title, pixels under 50% alpha are skipped

After each recording the window reports the loop seam: how many pixels differ between the last and first frame, next to how many change on an ordinary frame

If the seam is well below an ordinary frame, the loop is invisible

![VIGIL main menu](Documentation~/vigil-menu.gif)

*VIGIL's main menu: Hold pose, pixel scale 2, title overlay, ember particles looped (910 x 512, 48 frames, 44 KB)*

### How particle looping works

GIF Kit pauses every playing particle system, simulates it ahead in fixed steps and snapshots every particle

Each recorded frame shows only the particles born inside one loop window, with their ages wrapped around the loop, so whatever a system emits the last frame flows straight into the first

Particles that live longer than the loop work too, the snow in the banner lives 12 seconds inside a 9.6 second loop

## Tile

Generates a seamless background tile from dithered fog and snow-like flakes

The flakes wrap at the edges and fall a whole number of tiles per loop, so the GIF tiles in space and loops in time

Pick two colours, a size and a pixel scale, then save a PNG or GIF and use it as an itch.io page background with repeat on

![VIGIL page background](Documentation~/vigil-background.gif)

*VIGIL's page background, one tile (512 x 512, 128 frames, 278 KB)*

## GIF details

- Global palette with LZW compression, loops forever
- Frames after the first only store the pixels that changed and leave the rest transparent, which keeps long pixel-art loops small
- Browsers clamp frame delays under 20 ms, so anything above 50 FPS plays slower than recorded
