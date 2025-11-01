# Grass System Asset

## Overview

This asset provides a compute shader based grass system with adjustable wind sway and gust effects.

## Setup Instructions

Import the package.
Open the Grass Tool from Tools>Grass Tool.
Create a grass object from the tool.
Create Grass Settings as instructed in the tool, attach it to the tool.
In the grass settings file, choose the shader to use as GrassBlades.compute.
In the grass settings file, choose the material to use as `GrassMaterial`.
Create an empty GameObject called TerrainRenderer.
Attach `GrassCamera` prefab as a child to the TerrainRenderer.
Add `Render Terrain Map` script to the TerrainRenderer, Attach the surface/terrain to draw grass on to the script's lists. Choose the `GrassCamera` from the script and set the layer to `Default`.
Attach the `BelgiumGusts` texture from the GrassSystem/Textures/ to the `Grass System - Holder`'s `Wind Texture` variable.
Lower WindSpeed from the tool (to around 0.3)

## Credits

This asset is an improved and tweaked version of the grass system asset by Minionsart <https://www.patreon.com/minionsart>.

Author: @evefsect <https://github.com/evesfect>
