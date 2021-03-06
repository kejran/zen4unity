# Gothic Importer for Unity (zen4unity)

![preview](https://github.com/kejran/zen4unity/blob/master/preview.png?raw=true)

__zen4unity__ is a tool for loading Piranha Bytes' __Gothic__ and __Gothic II__ game assets directly into Unity. 

The importer interfaces directly with game archives and generates immediate Unity structures. There is no need for archive unpacker or 3d modelling software.

The tool relies on [zenglue](https://github.com/kejran/zenglue/), which in turns uses [ZenLib](https://github.com/Try/ZenLib) for all heavy lifting.

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

### Morph meshes
Convert morphing meshes (Ranged weapons, heads) into meshes with defined blendshapes.

### Materials and textures
All textures that loaded meshes depend on will be automatically extracted. A generic user material is cloned into separate materials for all textures - this way the importer should work with any rendering pipeline.

### Animations
All animations and their aliases can be imported as separate clips, to use with matching hierarchies. Embedded gothic mds events are exported into native animation events.

### Modelscript import
Modelscript files contain metadata that ties together hierarchies, meshes, animations, and effects related to a particular entity. Currently this allows for automatic binding of skinned meshes to bone hierarchies and binding animations to skeleton.

### Vobs
[Virtual Objects](https://regoth-project.github.io/REGoth-bs/case-studies/object-kinds.html) define a basic type of entity present in ZenGin. For our purposes, these represent a hierarchy of objects placed in the world - that is all vegetation, furniture, interactables, lights, trigger or audio zones, et cetera. 

All Vobs that contain children or have a defined visual are imported into Unity hierarchy during a level load. There is a WIP interactable/storage support.

### Waynet
Gothic uses a lightweight, graph-based navigation system. Nodes and edges are imported into a basic structure. Bring your own ai. 

### Prefabs
All Vobs imported can be turned into prefabs. That way, after a level import a specific item can be given some additional functionality, and the change will be propagated through the whole level.
Prefabs are also used to define bone hierarchies, and skinned models that use them become their prefab variants.

## TODO
The following features are planned for the future:
- Runtime character controller
- More extensive animation importer
- Automatic state machine generation
- Working interactables
- Lights
- Sounds

## Not in scope
### Daedalus
Gothic runs it logic on a homebrew VM machine. Integration of it is not a goal, which means the importer cannot gather information about placed items, npcs, chest contents, dialogs, etc.  

## Usage
* Open main window from main menu: [Gothic] -> [Open Importer]
* Select path to archives: this should be the Data folder inside the gothic installation
* Press [Verify] 
* Select gothic version 
* Setup default materials
* Select file type to import from dropdown (or use icon tabs in the next view)
* Switch to file browser tab to select a file, optionally use a filter to narrow down the selection
* Press import

## Notes
* Massive worlds (such as g2 overworld) are breaking the importer for now