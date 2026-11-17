# Reverberation energy field — milestone 1

This is an isolated visual experiment. It does not reference, replace or spawn gameplay Reverberations, apply damage, or interact with ResonanceResolver. Stop here for visual evaluation before refining the motion or adding depth/polish systems.

## Run and tune

1. Open `Assets/Scenes/TestScenes/ReverberationEnergyFieldTest.unity` and enter Play.
2. `01 - Single field (default)` starts active. Disable it before enabling `04 - Four fields` or `08 - Eight fields` to compare instance counts. These are ordinary hierarchy groups; no input/controller setup is needed.
3. Edit `Assets/Tuning/Experiments/ReverberationEnergyFieldTuning.asset`. Inspector changes refresh active instances and the editor preview automatically. Flow animates in Play mode; Scene view animation also depends on the editor's animated-material/refresh settings.
4. The reusable prefab is `Prefabs/ReverberationEnergyField.prefab` in this folder. Place its root at the ground position. Dimensions are local units before root scaling; the initial width/depth/height is 3 × 2 × 0.6.

Parameters are grouped into Field Shape, Energy Appearance, Background, Midground and Flow. Lower Noise Scale makes larger structures. Density widens/populates the currents; Opacity controls their transparency independently of Emission brightness. Brightness Bands quantizes the energy's brightness while opacity and edge fading remain soft. Field Dimensions, Layer Count (1–8), Layer Tilt and Edge Falloff control the approximate volume. All authored appearance parameters belong to the tuning SO; the material is shared wiring, not a second preset.

## Background and midground

The field now composites three components in the existing shader: a diffuse base from the two broad warp channels, wider connecting concentrations from the existing contour, and the original banded foreground on top. There are still three noise evaluations per fragment, the same sheets and the same draw calls. The new components share the existing Flow Speed. No additional meshes, materials or render passes are needed.

- **Background Color:** diffuse base colour, independent of foreground Emission so bright currents remain distinct. Its alpha also scales the background opacity.
- **Background Opacity:** strength of the ambient fill (initially 0.2).
- **Background Variation:** degree of broad colour/opacity fluctuation. At zero the base is uniform within the irregular edge envelope.
- **Midground Opacity:** strength of broader concentrations (initially 0.15). These inherit Energy Color, its alpha and Emission, at an intermediate brightness.
- **Midground Width:** how far the softer concentrations extend around the existing current contours (initially 0.4).

The two new opacities are normalized independently across the active sheet count, using the same formula as the original opacity. Spatial noise and alpha compositing still make the appearance differ between layer counts; normalization avoids simply multiplying each component's authored strength. All three components use the existing soft boundary envelope. Setting both new opacities to zero restores the original field. Existing authored foreground settings are preserved.

The fragment combines the components using alpha-over compositing, then converts to straight alpha for the material's existing blend mode. The foreground's noise, motion and brightness bands are unchanged. Background/midground opacity stays soft; no extra foreground gradient is introduced. Added work is scalar arithmetic and colour compositing, with no extra noise samples. The original performance table below predates this refinement and is not a new shader timing measurement.

## Animated boundary

**Boundary Motion** in the field tuning asset has two controls:

- **Edge Distortion Strength** (0–0.35, initially 0.18): amplitude of local perimeter expansion/contraction in normalized field coordinates. Zero restores the original mask exactly.
- **Edge Distortion Speed** (initially 0.3): speed of the additional edge noise, independent of internal Flow Speed. Zero freezes the added deformation; the original subtle flow-driven boundary irregularity is still present if internal flow is moving.

A single additional low-frequency sample reuses the existing value-noise function. It uses sheet UVs and a shared animation phase, rather than per-sheet seeds, so the layers follow a coherent boundary shape. A slight inward bias leaves room for expansion while keeping the overall footprint near its authored size. The resulting mask affects background, midground and foreground together; Edge Falloff still controls the main transition softness. Foreground noise, colour bands and flow calculations are unchanged.

With deformation enabled, an additional soft guard fades the last 10% of normalized distance to each quad edge, preventing a hard mesh cut at maximum strength. This reserves a small outer margin, and may constrain expansion at the extremities. The original mask bypasses this guard at strength zero for exact compatibility. Layer heights and tilts still create small projected differences; this remains a surface-based volume approximation.

There are four noise evaluations per fragment with boundary distortion enabled (three internal plus one boundary), versus the previous three. No geometry, materials, draw calls, CPU mesh animation or renderer changes were added. The old performance table is not a measurement of this refinement.

## Optional bloom preview

The test scene starts with `Bloom Preview` enabled. Toggle that GameObject to compare bloom on/off without changing the field. Edit `Assets/Tuning/Experiments/ReverberationEnergyFieldBloom.asset` for Intensity (initially 1), Threshold (0.8) and Scatter (0.65). The camera has HDR and post-processing enabled. This is ordinary scene-wide URP bloom after pixel upscaling: it adds a soft glow to bright pixels, including any bright environment objects, and does not pixelate the glow itself. The shader and field prefab remain independent of bloom.

## Implementation

`ReverberationEnergyField` controls a fixed pool of eight quad renderers, with four enabled by default. All sheets reuse Unity's built-in quad mesh and one material. Geometry placement and property blocks are updated only when the tuning asset/revision changes or the component is enabled. In steady state its CPU callback performs only a revision check. No materials or meshes are created every frame.

The shader evaluates two low-frequency warp channels and one contour-noise sample, with no octave loop, textures or fluid simulation. Contours provide luminous currents throughout the interior rather than a hollow ring. Broad noise modulates density, and a soft irregular elliptical mask fades well before the sheet boundary. Each height layer has a different seed, direction and speed; world-space noise gives separated instances different patterns. Three brightness bands are the default. GPU shader time drives the continuous flow.

The sheets are slightly tilted at different heights. Ordinary back-to-front transparency approximates the shallow volume for the fixed 45° camera. Depth-tested alpha blending uses `ZWrite Off`, `ZTest LEqual` and no shadows, distortion, particles, raymarching or VFX Graph. Bloom is an optional scene-level comparison.

## Pixel renderer

Every layer is on `PixelWorldVFX` (layer 12). Existing `PC_Renderer` already includes this layer in `transparentWorldLayers` (4096) and excludes it from URP's later Transparent Layer Mask (4294963199). Thus each sheet is rendered once at 640×360 before nearest-neighbour upscale. The test camera uses the actual 45° orthographic view with size 3.6. No renderer feature, renderer asset, pipeline setting or existing gameplay prefab was modified.

The floor, capsule and pillar are static depth/scale references, with a shared prototype environment material. The only authored post-processing override is the optional Bloom Preview volume.

## Measured baseline (without bloom)

Measured in Unity 6000.3.24f1 Editor batch Play mode, URP 17.3, OpenGL 4.5 on NVIDIA GeForce RTX 5070 Ti. Camera target: 1920×1080; internal world rendering: 640×360. VSync disabled; 60 warmup frames and 120 sampled frames per case. These are total-scene Editor CPU measurements, not standalone-build or isolated GPU shader timings.

| Instances | Active field sheets | Median PlayerLoop CPU | Median main thread | Total draw calls | Median wall frame |
|---:|---:|---:|---:|---:|---:|
| 0 | 0 | 0.933 ms | 0.982 ms | 28 | 0.984 ms |
| 1 | 4 | 1.035 ms | 1.083 ms | 32 | 1.085 ms |
| 4 | 16 | 1.072 ms | 1.124 ms | 44 | 1.127 ms |
| 8 | 32 | 1.119 ms | 1.170 ms | 60 | 1.171 ms |

The frame-wide GC counter median was 246 bytes in every case; this includes the Editor/test sampler and does not establish per-effect allocation cost. Hardware GPU frame timing was unavailable, so no GPU milliseconds or speedup claim is made. Each four-layer instance adds four measured draw calls. Profile a release build on target hardware after approving the visual direction.

## Checks and limitations

- Animated boundary compiled/rendered in isolated Unity Play mode. Strength zero matched the previous shader exactly at 1080p (0 changed RGB pixels). With internal Flow Speed zero, moving boundary noise changed 62,489 pixels over two simulation seconds; also zeroing Edge Distortion Speed produced identical captures. Maximum-strength output was rendered and inspected for soft mesh-edge fading. These checks do not establish shader GPU cost or visual quality at every view/setting.

- Background/midground refinement compiled and rendered in isolated Unity Play mode. With both new opacities zero, the 1080p capture matched the original shader exactly (maximum RGB difference 0/255). Property-block opacity normalization was checked at 1/4/8 layers. New shader GPU cost and subjective motion/appearance still require manual evaluation.

- Inspector tuning verified at 1/5/8/4 layers, including dimensions and property blocks; disable/re-enable lifecycle, shared material wiring and collider-free sheets checked.
- Unity compilation and shader rendering checked in an isolated project copy; captured 1080p stills through the real low-resolution renderer, including 1/4/8 instances and two times for flow comparison.
- This approximates depth with surfaces, not participating-media integration. It is tuned for the fixed gameplay camera; other viewing angles, intersections or overlapping fields may expose layering/sorting.
- The initial baseline used sparse, broad purple currents; the new background/midground controls fill the gaps, and the authored tuning remains user-controlled. It does not reproduce Control's fluid solver or Numen's raymarched strands. Those references guide appearance only; their assets/code were not imported.
- For milestone 2, judge current continuity, density and motion first. More convincing curls/branching or less visible layering can then be explored. No spawn, consumption, resonance or lifetime animation is implemented.
