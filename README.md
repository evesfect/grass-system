# GPU-Accelerated Grass System for Unity

A high-performance compute shader-based grass rendering system optimized for RTS and top-down games. Achieve dense grass coverage without sacrificing framerate.

## Features

-   **Compute shader-based rendering** - GPU-driven blade generation and animation (similar to the implementation of Sucker Punch: [https://www.youtube.com/watch?v=Ibe1JBF5i5Y](https://www.youtube.com/watch?v=Ibe1JBF5i5Y "https://www.youtube.com/watch?v=Ibe1JBF5i5Y"))
-   **Wind system** - Texture-based wind sway with wind gust support

    <video src="media/vid1.mp4" controls width="600"></video>

-   **Spatial culling** - Hierarchical frustum culling for optimal performance

    <video src="media/vid2.mp4" controls width="600"></video>

-   **Editor painting tools** - Intuitive brush-based grass placement with layers
-   **Customizable appearance** - Per-layer colors, dimensions, and density control
-   **Interactive grass** - Responds to shader interactors for gameplay effects

    <video src="media/vid3.mp4" controls width="600"></video>

## Performance

Designed for dense grass placement on small to medium-sized maps typical of RTS games, supports low-end hardware:

-   60,000+ grass blades rendered at 60+ FPS on Intel Iris XE graphics
-   Shadows enabled, alongside other scene assets
-   Spatial culling system scales to larger maps with distance-based LOD

## Setup Instructions

### Requirements

-   Unity 6000.0.34f1 or later
-   GPU with Comput Shader Support
-   URP render pipeline

### Installation

1.  Import the package into your Unity project
    
2.  Open the Grass Tool: `Tools > Grass Tool`
    
3.  Create a grass object from the tool
    
4.  Create Grass Settings:
    
    -   Follow instructions in the tool to create a settings asset
    -   Attach the settings to the tool
    -   In the grass settings file, set shader to `GrassBlades.compute`
    -   In the grass settings file, set material to `GrassMaterial`
5.  Set up terrain rendering:
    
    -   Create an empty GameObject called `TerrainRenderer`
    -   Attach the `GrassCamera` prefab as a child to `TerrainRenderer`
    -   Add `Render Terrain Map` script to `TerrainRenderer`
    -   Attach your terrain/surface to the script's terrain list
    -   Assign the `GrassCamera` from the script and set layer to `Default`
6.  Configure wind:
    
    -   Attach `BelgiumGusts` texture from `GrassSystem/Textures/` to the grass holder's `Wind Texture` variable
    -   Lower `Wind Speed` to around 0.3 for realistic movement
7.  Start painting grass using the Grass Tool window
    

## Usage

**Painting Grass:**

-   Select your grass object in the hierarchy
-   Use the Paint/Edit tab in the Grass Tool
-   Choose Add/Remove/Edit modes
-   Paint directly in the Scene view

**Adjusting Performance:**

-   Increase `cullingTreeDepth` for better culling (higher memory usage)
-   Adjust `maxDrawDistance` and `minFadeDistance` for LOD control
-   Reduce `allowedBladesPerVertex` and `allowedSegmentsPerBlade` for lower poly count

**Wind Settings:**

-   `windSpeed`: Base wind animation speed
-   `windTextureStrength`: Gust intensity
-   `windTextureScale`: Noise pattern scale for wind variation

## Technical Details

The system uses compute shaders to procedurally generate grass blades from painted vertex data. A hierarchical spatial partitioning structure (octree-like) performs frustum culling each frame, sending only visible grass instances to the GPU. Indirect rendering via `DrawProceduralIndirect` allows the GPU to handle geometry generation without CPU bottlenecks.

## Credits

This is an improved version of the grass system by [Minionsart](https://www.patreon.com/cw/minionsart "https://www.patreon.com/cw/minionsart"), featuring:

-   Enhanced shader visuals
-   Wind gust system (in addition to base wind sway)
-   Performance optimizations for dense placement scenarios
-   Various quality-of-life improvements

**Original System:** [Minionsart's Grass System](https://minionsart.github.io/tutorials/)**Enhanced by:** [@evesfect](https://github.com/evesfect)

Further credits to [forkercat](https://gist.github.com/forkercat "https://gist.github.com/forkercat") and [NedMakesGames](https://github.com/NedMakesGames "https://github.com/NedMakesGames") for the contributions in the initial project

## License

See [LICENSE](LICENSE) file for details.
