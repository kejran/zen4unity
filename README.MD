# Gothic Importer for Unity (zen4unity)

![preview](https://github.com/kejran/zen4unity/blob/master/preview.png?raw=true)

__zen4unity__ is a tool for loading Piranha Bytes' __Gothic__ and __Gothic II__ game assets directly into Unity. 

The importer interfaces directly with game archives and generates immediate Unity structures. There is no need for archive unpacker or 3d modelling software.

The tool relies on [zenglue](https://github.com/kejran/zenglue/), which in turns uses [ZenLib](https://github.com/ataulien/ZenLib) [(OpenGothic)](https://github.com/Try/ZenLib) for all heavy lifting.

## Features

### Static meshes
Load any static object as native unity mesh and store it as an asset. Any non-animated mesh can be loaded: 
* Inventory items: swords, potions, rings, runes...
* Furniture
* Vegetation
* Static interactables: beds, campfires...

### World mesh
Load entire level into a multi-material mesh asset. 

### Skinned meshes
Import bone hierarchies and skinned models that can be posed and animated.

### Materials and textures
All textures that loaded meshes depend on will be automatically extracted. A generic user material is cloned into separate materials for all textures - this way the importer should work with any rendering pipeline.

### Modelscript import
Modelscript files contain metadata that ties together hierarchies, meshes, animations, and effects related to a particular entity. Currently this allows for automatic binding of skinned meshes to bone hierarchies.

### Vobs
[Virtual Objects](https://regoth-project.github.io/REGoth-bs/case-studies/object-kinds.html) define a basic type of entity present in ZenGin. For our purposes, these represent a hierarchy of objects placed in the world - that is all vegetation, furniture, interactables, lights, trigger or audio zones, et cetera. 

All Vobs that contain children or have a defined visual are imported into Unity hierarchy during a level load.  

### Prefabs
All Vobs imported can be turned into prefabs. That way, after a level import a specific item can be given some additional functionality, and the change will be propagated through the whole level.
Prefabs are also used to define bone hierarchies, and skinned models that use them become their prefab variants.

## TODO
The following features are planned for the future:
- Interactive meshes
- Animations
- Animation events
- Lights
- Navigation graph

## Not in scope
### Daedalus
Gothic runs it logic on a homebrew VM machine. Integration of it is not a goal, which means the importer cannot gather information about placed items, npcs, chest contents, dialogs, etc.  

## Usage
* Open main window from main menu: [Gothic] -> [Open Importer]
* Select path to archives: this should be the Data folder inside the gothic installation
* Press [Verify] 
* Select gothic version and type of import
* Setup material options
* Select file type to import from dropdown (or use icon tabs in the next view)
* Switch to file browser tab to select a file,optionally use a filter to narrow down the selection
* Press import

## Notes
* Semi-Transparent textures are WIP
* Massive worlds (such as g2 overworld) are breaking the importer for now