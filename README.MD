# Gothic Importer for Unity (zen4unity)

_(preview image here)_

__zen4unity__ is a tool for loading Piranha Bytes' __Gothic__ and __Gothic II__ game assets directly into Unity. 

The importer interfaces directly with game archives and generates immediate Unity structures. There is no need for archive unpacker or 3d modelling software.

The tool relies on [zenglue](https://github.com/kejran/zenglue/), which in turns uses [ZenLib](https://github.com/ataulien/ZenLib) [(OpenGothic)](https://github.com/Try/ZenLib) for all heavy lifting.

## Features

- Static meshes
- Materials and textures
- World mesh
- VOB hierarchy
- Mesh prefab packing

## TODO
- Skinned meshes
- Interactive meshes
- Animations

## Not in scope
- Daedalus VM

## Usage
* Open main window from main menu: [Gothic] -> [Open Importer]
* Select path to archives: this should be the Data folder inside the gothic installation
* Press [Verify] 
* Select gothic version and type of import
* Setup material options
* Select file to import from dropdown, optionally use filter to narrow down the selection
* Press import

## Notes
* Semi-Transparent textures are WIP
* Massive worlds (such as g2 overworld) are breaking the importer for now