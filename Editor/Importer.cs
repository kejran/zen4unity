using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;

using ZenGlue;

#nullable enable

public class Importer : IDisposable
{
    VDFS vdfs;
    Zen? zen;
    string root;

    private enum  PrefabType {
        StaticMesh,
        Skeleton,
        SkinnedMesh,
        DynamicMesh,
        ModelScript
    }

    public Importer(string assetRoot = "") {
        root = assetRoot;
        vdfs = new VDFS();
    }

    public string[] AllFiles()
    {
        return vdfs.Files();
    }

    private string pathJoin(params string[] paths) {
        return String.Join("/", paths);
    }

    public void LoadArchives(string basePath, string[] archives)
    {
        foreach (var f in archives)
            vdfs.LoadArchive(pathJoin(basePath, f));

        vdfs.FinalizeLoad();
    }

    public struct MeshLoadSettings
    {
        public bool loadMaterials;
        public MaterialLoadSettings materialSettings;
    }

    public struct MaterialLoadSettings
    {
        public Material opaqueMaterialTemplate;
        public Material transparentMaterialTemplate;
        public bool loadTextures;
    }

    public struct PrefabLoadSettings
    {
        public bool loadStatic;
        public bool loadScripts;
        public bool loadStructural;
    }

    public void LoadWorld(string world, bool forceG2)
    {
        if (zen != null) zen.Dispose();
        zen = new Zen(vdfs, world, forceG2);
    }

    private void makeDir(string name)
    {
        System.IO.Directory.CreateDirectory(pathJoin(root, name));
    }

    private T loadAsset<T>(string path) where T : UnityEngine.Object
    {
        return (T)AssetDatabase.LoadAssetAtPath(path, typeof(T));
    }

    private Texture2D makeTexture(string name, string assetName)
    {
        var path = pathJoin(root, "Textures", assetName);
        var asset = loadAsset<Texture2D>(path);
        if (asset != null) return asset;

        using (var ztex = new ZTexture(vdfs, name))
        {
            var result = ztex.toUnityTexture();
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
    }

    private Texture2D? tryGetTexture(string texture)
    {
        if (vdfs.Exists(texture))
            try
            {
                return makeTexture(texture, texture.Replace("-C.TEX", ".asset"));
            }
            catch (Exception e)
            {
                Debug.LogWarning(e);
            }
        else
            Debug.LogWarning($"Missing texture: '{texture}'. Check if proper VDFS archives are included.");
        return null;
    }

    private Material makeMaterial(string texture, MaterialLoadSettings settings, Color? color = null)
    {
        bool validName = texture != "";
        if (!validName)
            if (color != null)
            {
                Color32 cc = (Color)color;
                texture = $"UNNAMED({cc.r},{cc.g},{cc.b},{cc.a})";
            }
            else
                texture = "UNNAMED";
        var path = pathJoin(root, "Materials", texture.Replace(".TGA", "") + ".mat");
        var stored_mat = loadAsset<Material>(path);
        if (stored_mat != null)
            return stored_mat;

        Texture2D? tex2d = null;
        if (settings.loadTextures && texture.Contains(".TGA")) // todo why is the tga part here?
        {
            var compressed = texture.Replace(".TGA", "-C.TEX");
            tex2d = tryGetTexture(compressed);
        }

        var useTransparent = settings.loadTextures && tex2d != null && (
            tex2d.format == TextureFormat.DXT5 || tex2d.format == TextureFormat.RGBA32);
        var baseMaterial = useTransparent ? settings.transparentMaterialTemplate : settings.opaqueMaterialTemplate;

        if (baseMaterial == null) throw new Exception("Base material was not provided");
        var mat = new Material(baseMaterial);

        if (tex2d != null)
            mat.mainTexture = tex2d;
        else if (color != null)
            mat.color = (Color)color;

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public Material MakeMaterial(string texture, MaterialLoadSettings settings) {
        if (settings.loadTextures)
            makeDir("Textures");
        makeDir("Materials");

        // todo clean up this extension mess
        return makeMaterial(texture.Replace("-C.TEX", "").Replace(".TGA", "") + ".TGA", settings);
    }

    Material[] makeMaterials(MaterialMesh zmesh, MaterialLoadSettings settings)
    {
        if (settings.loadTextures)
            makeDir("Textures");
        makeDir("Materials");

        var submeshes = zmesh.submeshCount();
        var materials = new Material[submeshes];

        for (uint i = 0; i < submeshes; ++i)
            materials[i] = makeMaterial(zmesh.texture(i), settings, zmesh.color(i));

        return materials;
    }

    private Mesh makeMesh(ZMesh zmesh)
    {
        var umesh = new Mesh();

        umesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        umesh.vertices = zmesh.vertexPositions();
        umesh.normals = zmesh.vertexNormals();
        umesh.uv = zmesh.vertexUVs();

        var submeshes = zmesh.submeshCount();
        bool showProgress = submeshes > 4;
        umesh.subMeshCount = (int)submeshes;
        for (uint i = 0; i < submeshes; ++i) {
            if (showProgress)
                EditorUtility.DisplayProgressBar("Mesh import", "Importing...", i / (float)submeshes);
            umesh.SetTriangles(zmesh.submeshElements(i), (int)i);
        }
        if (showProgress)
            EditorUtility.ClearProgressBar();
        return umesh;
    }

    private Mesh makeSkinMesh(ZSkinnedMesh zmesh, Transform[] tPose)
    {
        var umesh = new Mesh();

        umesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var weights = zmesh.boneWeights();
        umesh.vertices = zmesh.bindPoseVertices(tPose, weights);
        umesh.boneWeights = weights;
        umesh.bindposes = tPose.Select(t => t.worldToLocalMatrix).ToArray();
        umesh.normals = zmesh.vertexNormals();
        umesh.uv = zmesh.vertexUVs();

        var submeshes = zmesh.submeshCount();
        umesh.subMeshCount = (int)submeshes;
        for (uint i = 0; i < submeshes; ++i)
            umesh.SetTriangles(zmesh.submeshElements(i), (int)i);
        return umesh;
    }

    void setTransform(Transform t, Matrix4x4 mat) {
        t.localPosition = new Vector3(mat.m03, mat.m13, mat.m23) * 0.01f;
        t.localRotation = mat.rotation;
        t.localScale = mat.lossyScale;
    }

    Transform[] iterNodes(Transform? parent, Transform[] all, ZMeshLib.Node[] nodes) {
        return nodes.Select(n => {
            var go = new GameObject(n.name);
            var t = go.transform;
            if (parent != null)
                t.SetParent(parent);
            setTransform(t, n.transform);
            all[n.index] = t;
            iterNodes(t, all, n.children);
            return t;
        }).ToArray();
    }

    private Tuple<Transform, Transform[]> makeSkeleton(ZMeshLib lib, bool insertRoot, string idleHint) {
        var nodeInfo = lib.Nodes();
        var nodes = nodeInfo.asTree;
        var arr = new Transform[nodeInfo.asArray.Length];

        // it is useful to error if the tree is empty even with wrapper, since it should never happen
        if (nodes.Length == 0)
            throw new Exception("No root nodes found");
        if (!insertRoot)
            if (nodes.Length > 1) {
                var s = "Expected 1 root node, got: [" + nodes[0].name;
                foreach (var nn in nodes.Skip(1))
                    s += ", " + nn.name;
                throw new Exception(s + "]");
            }
        // todo: this can be done more clearly, most likely...
        var newParent = insertRoot ? new GameObject().transform : null;
        var res = iterNodes(newParent, arr, nodes);
        
        if (insertRoot && idleHint != "")
        {
            var file = findIdleAnim(idleHint);
            if (file != "") 
                using (var ani = new ZAni(vdfs, file))
                {
                    var indices = ani.nodeIndices();
                    uint root = 0xffff;
                    for (uint i = 0; i < indices.Length; ++i)
                        if (indices[i] == 0)
                            root = i;
                    if (root != 0xffff)
                    {
                        var pose = ani.packedSamples()[root];
                        res[0].position = pose.position; 
                        // todo: maybe add rotation? probably not useful
                    }
                }
        }

        return Tuple.Create(newParent != null ? newParent : res[0], arr);
    }

    private string findIdleAnim(string visual) 
    {
        visual = Path.GetFileNameWithoutExtension(visual);
        var candidates = new string[]{"S_RUN", "S_FISTRUN", "S_S0"};
        foreach (var c in candidates)
        {
            var file = visual + "-" + c + ".MAN";
            if (vdfs.Exists(file))
                return file;
        }
        return "";
    }

    private GameObject importSkeleton(ZMeshLib lib, string visual, bool insertRoot)
    {
        visual = Path.GetFileNameWithoutExtension(visual);
        var path = pathJoin(root, "Avatars", visual + ".asset");
        var (rootBone, allBones) = makeSkeleton(lib, insertRoot, visual);
        var go = rootBone.gameObject;
        var avatar = AvatarBuilder.BuildGenericAvatar(go, rootBone.name); // dummy root ? or the bip?
        makeDir("Avatars");
        AssetDatabase.CreateAsset(avatar, path);

        var ani = go.AddComponent<Animator>();
        ani.avatar = avatar;

        var rend = go.AddComponent<SkinnedMeshRenderer>();
        rend.bones = allBones;

        return packageAsPrefab(go, "Rigs", visual);
    }

    private GameObject? importSkin(ZMeshLib lib, string skeleton, string visual, MeshLoadSettings settings)
    {
        visual = Path.GetFileNameWithoutExtension(visual);
        var path = pathJoin(root, "Meshes/Skins", visual + ".asset");
        makeDir("Meshes/Skins");
        var go = getOrMakePrefab(skeleton, PrefabType.Skeleton, settings);

        if (go == null)
            return null;

        var rend = go.GetComponent<SkinnedMeshRenderer>();

        using(var zmesh = lib.SkinnedMesh()) {
            var mesh = makeSkinMesh(zmesh, rend.bones);
            AssetDatabase.CreateAsset(mesh, path);
            rend.sharedMesh = mesh;

            if (settings.loadMaterials)
                rend.materials = makeMaterials(zmesh, settings.materialSettings);
        }

        return packageAsPrefab(go, "Models/Skins", visual);
    }

    bool isStatic(Vector3[] array) {
        var first = array[0];
        for (int i = 1; i < array.Length; ++i)
            if ((array[i] - first).sqrMagnitude > 0.001f)
                return false;
        return true;
    }

    bool isStatic(Quaternion[] array) {
        var first = array[0];
        for (int i = 1; i < array.Length; ++i)
            if (Mathf.Abs(Quaternion.Dot(array[i], first)) > 0.001f)
                return false;
        return true;
    }

    private void importAnimation(ZAni zani, string skeleton, string assetName, bool dummyRoot)
    {
        var go = getOrMakePrefab(skeleton, PrefabType.Skeleton, new MeshLoadSettings());
        if (go == null)
        {
            Debug.LogWarning("Failed to find skeleton" + skeleton);
            return;
        }

        var sp = assetName.Split('-', 2);
        var path = pathJoin(root, "Animations", sp[0], sp[1].Replace(".MAN", "") + ".asset");
        makeDir("Animations/" + sp[0]);

        var ani = new AnimationClip();

        var nodes = zani.nodeIndices();
        var frames = zani.frames();
        var samples = zani.packedSamples();
        float invFps = 1.0f / zani.fps();

        var nextAni_ = zani.next().ToUpper();
        var nextAniName = nextAni_.Length > 0 ? Path.GetFileNameWithoutExtension(skeleton) +
            "-" + nextAni_ + ".MAN" : nextAni_;

        // todo add looping/next dummy frame at the end

        if (frames == 0)
        {
            Debug.LogWarning(assetName + " is empty");
            return;
        }

        var skin = go.GetComponent<SkinnedMeshRenderer>();

        AnimationCurve? rootMotionX = null;
        AnimationCurve? rootMotionZ = null;
        
        var animationRoot = dummyRoot ? go.transform.GetChild(0) : go.transform;

        // iterate for all (except dummy root)
        for (uint node = 0; node < nodes.Length; ++node) {
            var t = skin.bones[nodes[node]];
            var rel = AnimationUtility.CalculateTransformPath(t, go.transform);

            var curves = new AnimationCurve[7];
            for (int i = 0; i < 7; ++i)
                curves[i] = new AnimationCurve();

            var translation0 = samples[node].position;
            bool encodeTranslation = true;//false; //todo test it
            bool isRoot = t == animationRoot;
            void setCurve(string name, AnimationCurve curve) {
                if (isRoot && dummyRoot)
                {
                    if (name == "localPosition.x")
                    {
                        rootMotionX = curve;
                        return;
                    }
                    if (name == "localPosition.z")
                    {
                        rootMotionZ = curve;
                        return;
                    }
                }
                ani.SetCurve(rel, typeof(Transform), name, curve);
            }

            for (uint s = 0; s < frames; ++s) {
                var sample = samples[nodes.Length * s + node];
                float time = s * invFps;

                if ((translation0 - sample.position).sqrMagnitude > 0.001f)
                    encodeTranslation = true;

                curves[0].AddKey(time, sample.position.x);
                curves[1].AddKey(time, sample.position.y);
                curves[2].AddKey(time, sample.position.z);

                curves[3].AddKey(time, sample.rotation.x);
                curves[4].AddKey(time, sample.rotation.y);
                curves[5].AddKey(time, sample.rotation.z);
                curves[6].AddKey(time, sample.rotation.w);
            }

            if (encodeTranslation)
            {
                setCurve("localPosition.x", curves[0]);
                setCurve("localPosition.y", curves[1]);
                setCurve("localPosition.z", curves[2]);
            } else
            {
                float end = invFps * (frames - 1);
                setCurve("localPosition.x", AnimationCurve.Constant(0, end, translation0.x));
                setCurve("localPosition.y", AnimationCurve.Constant(0, end, translation0.y));
                setCurve("localPosition.z", AnimationCurve.Constant(0, end, translation0.z));
            }

            setCurve("localRotation.x", curves[3]);
            setCurve("localRotation.y", curves[4]);
            setCurve("localRotation.z", curves[5]);
            setCurve("localRotation.w", curves[6]);
        }

        if (rootMotionX != null)
            ani.SetCurve("", typeof(Transform), "localPosition.x", rootMotionX);
        if (rootMotionZ != null)
            ani.SetCurve("", typeof(Transform), "localPosition.z", rootMotionZ);

        ani.EnsureQuaternionContinuity();
        
        AssetDatabase.CreateAsset(ani, path);

        GameObject.DestroyImmediate(go);

        //return packageAsPrefab(go, "Prefabs/Skins", assetName);
    }

    private void makeMorphs(Mesh mesh, uint[] vertexIds, ZMorph.Blend[] morphs) {

        // vertexIds = currentId -> originalId
        // ani = [originalid]

        int counter = 0;
        foreach (var morph in morphs)
        {
            ++counter;
            var vertexCount = morph.indices.Length;
            float invTime = 1.0f / morph.frames;

            for (int f = 0; f < morph.frames; ++f)
            {
                var update = new Dictionary<uint, Vector3>();
                for (int v = 0; v < vertexCount; ++v)
                    update[morph.indices[v]] = morph.vertices[v + vertexCount * f];

                var delta = new Vector3[mesh.vertexCount];
                for (uint v = 0; v < delta.Length; ++v) {

                    if (update.TryGetValue(vertexIds[v], out var value))
                        delta[v] = value;
                    else
                        delta[v] = Vector3.zero;
                }
                var name = morph.name + "@" + counter.ToString(); // todo handle better?
                mesh.AddBlendShapeFrame(name, (1 + f) * invTime, delta, null, null);
            }
        }
    }


    private GameObject importMeshImplMorph(
        ZMesh zmesh, MeshLoadSettings settings, string visual,
        ZMorph.Blend[] morphs
    ) {
        visual = Path.GetFileNameWithoutExtension(visual);
        var folder = "Meshes/Morphs";
        var umesh = makeMesh(zmesh);
        var path = pathJoin(root, folder, visual + ".asset");
        makeDir(folder);

        makeMorphs(umesh, zmesh.vertexIds(), morphs);

        //hack around broken blenshapes, wtf????
        //https://issuetracker.unity3d.com/issues/blendshapes-added-with-mesh-dot-addblendshapeframe-are-not-updated-correctly-when-modifying-the-offsets
        umesh.vertices = umesh.vertices;

        AssetDatabase.CreateAsset(umesh, path);

        var go = new GameObject();
        go.name = visual;

        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = umesh;

        if (settings.loadMaterials)
            smr.materials = makeMaterials(zmesh, settings.materialSettings);

        return go;

    }

    private GameObject importMeshImplObj(
        ZMesh zmesh, MeshLoadSettings settings, string visual,
        bool addToExistingAsset, GameObject? existingParent
    ) {
        var folder = existingParent == null ? "Meshes/Static" : "Meshes/Dynamic";
        // hack, do it in a clearer way

        var umesh = makeMesh(zmesh);
        var path = pathJoin(root, folder, visual + ".asset");
        makeDir(folder);

        if (addToExistingAsset)
            AssetDatabase.AddObjectToAsset(umesh, path);
        else
            AssetDatabase.CreateAsset(umesh, path);

        GameObject go;
        if (existingParent == null)
        {
            go = new GameObject();
            go.name = visual;
        } else
            go = existingParent;

        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = umesh;

        var mr = go.AddComponent<MeshRenderer>();

        if (settings.loadMaterials)
            mr.materials = makeMaterials(zmesh, settings.materialSettings);

        return go;
    }

    private GameObject packageAsPrefab(GameObject go, string path, string assetName)
    {
        makeDir(path);
        // todo use SaveAsPrefabAssetAndConnect instead
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, pathJoin(root, path, assetName + ".prefab"));
        AssetDatabase.Refresh();
        GameObject.DestroyImmediate(go);
        return prefab;
    }

    private GameObject importMeshImpl(ZMesh zmesh, MeshLoadSettings settings, string visual)
    {
        visual = Path.GetFileNameWithoutExtension(visual);
        var go = importMeshImplObj(zmesh, settings, visual, false, null);
        return packageAsPrefab(go, "Models/Static", visual);
    }

    private GameObject importMorph(ZMesh zmesh, MeshLoadSettings settings, string visual, ZMorph.Blend[] blends)
    {
        visual = Path.GetFileNameWithoutExtension(visual);
        var go = importMeshImplMorph(zmesh, settings, visual, blends);
        return packageAsPrefab(go, "Models/Morphs", visual);
    }

    private GameObject[] importVOBs(ZVOB[] vobs, MeshLoadSettings sMesh, PrefabLoadSettings sPrefab)
    {
        var result = new List<GameObject>();
        foreach (var vob in vobs)
        {
            var children = importVOBs(vob.children(), sMesh, sPrefab);

            GameObject? obj = null;
            var name = vob.name();

            var visual = vob.visual().ToUpper();

            if (name == "" && visual != "")
                name = visual;

            if (visual.EndsWith("3DS")) 
            {
                if (sPrefab.loadStatic) 
                {
                    obj = getOrMakePrefab(visual, PrefabType.StaticMesh, sMesh);
                    if (obj == null)
                        Debug.LogWarning("Could not find " + visual);
                }
            }

            else if (visual.EndsWith("ASC")) 
            {
                if (sPrefab.loadStructural) 
                {
                    obj = getOrMakePrefab(visual, PrefabType.DynamicMesh, sMesh);
                    if (obj == null)
                        Debug.LogWarning("Could not find " + visual);
                }
            }

            else if (visual.EndsWith("MDS"))
            {
                if (sPrefab.loadScripts)
                {
                    obj = getOrMakePrefab(visual, PrefabType.ModelScript, sMesh);
                    if (obj == null)
                        Debug.LogWarning("Could not find " + visual);
                }
            }

            else if (visual.EndsWith("PFX"))
            {
                // todo: particle effect vobs
            }

            else if (visual.EndsWith("TGA"))
            {
                // todo: textured quads ? find out what this is
            }

            else if (visual.EndsWith("MMS"))
            {
                // todo: directly import morph (waving water plant in g1)
            }

            else if (visual != "")
                Debug.LogWarning("Unknown visual type: " + visual);

            // make dummy objects for holding children
            if (obj == null && children.Length > 0)
                obj = new GameObject();

            var isLockable = vob.type() == ZVOB.Type.MobDoor || vob.type() == ZVOB.Type.MobContainer;

            if ((vob.type() == ZVOB.Type.MobInter || isLockable) && obj != null) // todo hack to skip unselected objects, improve later
            {
                if (obj == null)
                    obj = new GameObject();
                var inter = obj.AddComponent<zen4unity.Vob.Interactable>();
                //inter.requiredItem = 

                if (isLockable && vob.locked()) // looks like there is one chest with set code and locked = false in g1? 
                {
                    var lockable = obj.AddComponent<zen4unity.Vob.Lockable>();
                    lockable.lockCode = vob.lockCode();
                    lockable.keyId = vob.lockKey();
                }

                if (vob.type() == ZVOB.Type.MobContainer) 
                {
                    var container = obj.AddComponent<zen4unity.Vob.Container>();
                    
                    var items = new List<zen4unity.Vob.Container.Stack>();
                    var item = new zen4unity.Vob.Container.Stack();
                    var itemString = vob.containerContents();
                    if (itemString != "")
                        foreach (var str in itemString.Split(new char[] {',', '.', ';'})) 
                        {
                            var split = str.Split(':');
                            item.id = split[0];
                            item.count = split.Length > 1 ? UInt32.Parse(split[1]) : 1;
                            items.Add(item);
                        }
                    container.items = items.ToArray();
                }
            }

            if (obj != null)
            {
                obj.transform.position = vob.position();
                obj.transform.rotation = vob.rotation();
                result.Add(obj);

                if (children.Length > 0)
                    foreach (var child in children)
                        child.transform.SetParent(obj.transform, false);

                if (name != "")
                    obj.name = name;
            }
        }
        return result.ToArray();
    }

    private GameObject importDynamic(ZMeshLib lib, MeshLoadSettings settings, string visual, string skeletonHint = "")
    {
        visual = Path.GetFileNameWithoutExtension(visual);
        var attached = lib.Attached();
        var dict = new Dictionary<string, ZMesh>();
        foreach (var (name, mesh) in attached) {
            // should not happen, there are barely any hierarchies...
            if (dict.ContainsKey(name))
                Debug.LogWarning("Dropping duplicate attached mesh for bone: " + name);
            dict[name] = mesh;
        }

        Transform skeleton;
        if (lib.hasNodes()) // use embedded skeleton directly
            skeleton = makeSkeleton(lib, false, visual).Item1; // todo this probably should not be embedded; we want a rig?
            // todo decide whether to include root node 
        else
        {
            var skPath = findSkeleton(skeletonHint != "" ? skeletonHint : visual);
            if (skPath == "")
                throw new Exception("Failed to find skeleton for " + visual); // this should not happen with sane files
            using (var slib = new ZMeshLib(vdfs, skPath))
                skeleton = makeSkeleton(slib, true, skPath).Item1;
        }

        bool assetCreated = false;
        void iter(Transform obj) {
            if (dict.TryGetValue(obj.name, out var mesh))
            {
                dict.Remove(obj.name);
                importMeshImplObj(mesh, settings, visual, assetCreated, obj.gameObject);
                assetCreated = true;
            }
            for (int i = 0; i < obj.childCount; ++i)
                iter(obj.GetChild(i));
        }
        iter(skeleton);

        foreach(var (name, mesh) in dict)
            Debug.LogWarning("Found non-attached mesh " + name + " in " + visual);

        foreach (var (name, mesh) in attached)
            mesh.Dispose();

        var go = new GameObject(visual);
        skeleton.SetParent(go.transform, false);

        return packageAsPrefab(go, "Models/Dynamic", visual);
    }

    private GameObject? importScriptModel(ZScript script, MeshLoadSettings settings, string visual) {

        // todo: animator controller and custom script to handle states

        visual = Path.GetFileNameWithoutExtension(visual);

        var mt = script.meshTree();
        if (mt == "")
            return null;

        var skin = findSkin(mt);
        if (skin == "")
            return null;

        GameObject go;
        using (var lib = new ZMeshLib(vdfs, skin))
            go = instantiate(importDynamic(lib, settings, visual));

        // ignore registered meshes?
        foreach (var r in script.registeredMeshes())
            Debug.LogWarning("Interactable " + visual + " has a registered mesh: " + r);
        // ignore anims for now        
        //var anims = zscript.getAnis();
        
        return packageAsPrefab(go, "Models/Scripts", visual);
    }

    private GameObject? getOrMakePrefab(string visual, PrefabType type, MeshLoadSettings settings)
    {
        var subdir = type switch {
            PrefabType.Skeleton => "Rigs",
            PrefabType.StaticMesh => "Models/Static",
            PrefabType.SkinnedMesh => "Models/Skins",
            PrefabType.DynamicMesh => "Models/Dynamic",
            PrefabType.ModelScript => "Models/Scripts",
            _ => ""
        };

        visual = Path.GetFileNameWithoutExtension(visual); // always clear the extension to be safe
        var path = pathJoin(root, subdir, visual + ".prefab");
        var prefab = loadAsset<UnityEngine.Object>(path);
        if (prefab == null)
        {
            if (type == PrefabType.StaticMesh) // MRM
            {
                var file = visual + ".MRM";
                if (!vdfs.Exists(file))
                    return null;
                using (var zmesh = new ZMesh(vdfs, file))
                    prefab = importMeshImpl(zmesh, settings, visual);
            }
            if (type == PrefabType.Skeleton) // MDL, MDH
            {
                var file = findSkeleton(visual);
                if (file == "")
                    return null;
                using (var lib = new ZMeshLib(vdfs, file))
                    prefab = importSkeleton(lib, visual, true);
            }
            if (type == PrefabType.DynamicMesh) // MDL, MDM
            {
                var file = findSkin(visual);
                if (file == "")
                    return null;
                using (var lib = new ZMeshLib(vdfs, file))
                    prefab = importDynamic(lib, settings, visual);
            }
            if (type == PrefabType.ModelScript) // MDS, MSB
            {
                var file = findScript(visual);
                if (file == "")
                    return null;
                using (var script = new ZScript(vdfs, file))
                    prefab = importScriptModel(script, settings, visual);
            }
        }

        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    public GameObject instantiate(GameObject prefab) {
        var result = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (result == null)
            throw new Exception("Failed to instantiate prefab");
        else
            return result;
    }

    public void ImportWorldMesh(MeshLoadSettings settings)
    {
        var zmesh = zen!.mesh();
        if (zmesh != null)
        {
            PrefabUtility.InstantiatePrefab(importMeshImpl(zmesh, settings, zen.Name));
            zmesh.Dispose();
        }
    }

    public void ImportVobs(MeshLoadSettings sMesh, PrefabLoadSettings sPrefab) {
        importVOBs(zen!.data().vobs(), sMesh, sPrefab);
    }

    public void ImportWaynet() {
        var go = new GameObject();
        var r = go.AddComponent<zen4unity.WaynetRenderer>();
        r.waynet = zen!.data().waynet();
    }

    public GameObject ImportMesh(string name, MeshLoadSettings settings)
    {
        using (var zmesh = new ZMesh(vdfs, name))
            return instantiate(importMeshImpl(zmesh, settings, name));
    }

    public GameObject ImportMorph(string name, MeshLoadSettings settings)
    {
        using (var zmorph = new ZMorph(vdfs, name))
            using (var zmesh = zmorph.mesh())
                return instantiate(importMorph(zmesh, settings, name, zmorph.blends()));
    }

    public GameObject ImportDynamic(string name, MeshLoadSettings settings)
    {
        using (var lib = new ZMeshLib(vdfs, name))
            return instantiate(importDynamic(lib, settings, name));
    }

    public GameObject ImportSkeleton(string name)
    {
        using (var lib = new ZMeshLib(vdfs, name))
            return instantiate(importSkeleton(lib, name, true)); // todo: vary whether to include root node? make a toggle?
    }

    public string findSkin(string name) {
        var n = Path.GetFileNameWithoutExtension(name);
        var mdl = n + ".MDL";
        var mdm = n + ".MDM";
        if (vdfs.Exists(mdl))
            return mdl;
        if (vdfs.Exists(mdm))
            return mdm;
        return "";
    }

    public string findSkeleton(string visual) 
    {       
        visual = Path.GetFileNameWithoutExtension(visual);
        var mdh = visual + ".MDH";
        if (vdfs.Exists(mdh))
            return mdh;
        var mdl = visual + ".MDL";
        if (vdfs.Exists(mdl))
            return mdl;
        return "";
    }

    public string findScript(string name) {
        var n = Path.GetFileNameWithoutExtension(name);
        var msb = n + ".MSB";
        var mds = n + ".MDS";
        if (vdfs.Exists(msb))
            return msb;
        if (vdfs.Exists(mds))
            return mds;
        return "";
    }

    public GameObject? ImportSkin(string name, string skeletonAsset, MeshLoadSettings settings)
    {
        if (!vdfs.Exists(name))
            return null;
        using (var lib = new ZMeshLib(vdfs, name))
        {
            var skin = importSkin(lib, skeletonAsset, name, settings);
            if (skin != null)
                return instantiate(skin);
            else
                return null;
        }
    }

    public GameObject? ImportSkinOrDynamic(string name, string skeletonAsset, MeshLoadSettings settings)
    {
        if (!vdfs.Exists(name))
            return null;
        using (var lib = new ZMeshLib(vdfs, name))
        {
            if (lib.hasAttachments())
                return instantiate(importDynamic(lib, settings, name, skeletonAsset));
            else
            {
                var skin = importSkin(lib, skeletonAsset, name, settings);
                if (skin != null)
                    return instantiate(skin);
                else
                    return null;
            }
        }
    }

    public void ImportAnimation(string name, string skeletonAsset)
    {// todo add dummy root toggle
        using (var lib = new ZAni(vdfs, name))
            /*PrefabUtility.InstantiatePrefab(*/importAnimation(lib, skeletonAsset, name, true);//);
    }

    public class ScriptData
    {
        public string hierarchy = "";
        public string baseMesh = "";
        public string[] registeredMeshes = {};
        public string[] anims = {};
    }

    public ScriptData ImportScript(string name)
    {
        using (var zscript = new ZScript(vdfs, name)) {
            var result = new ScriptData();
            result.hierarchy = findSkeleton(name);
            result.baseMesh = zscript.meshTree().ToUpper().Replace(".ASC", "");
            result.registeredMeshes = zscript.registeredMeshes().Select(x => x.ToUpper().Replace(".ASC", "")).ToArray();
            var anims = zscript.getAnis();
            var aniascs = new HashSet<string>();
            foreach (var a in anims)
                if (a.asc != "")
                    aniascs.Add(System.IO.Path.GetFileNameWithoutExtension(a.name.ToUpper()));
            result.anims = aniascs.ToArray().OrderBy(x => x.Length > 2 && x[1] == '_' ? x.Substring(2) : "_" + x).ToArray();
            return result;
        }
    }

    public void Dispose()
    {
        if (zen != null)
            zen.Dispose();
        if (vdfs != null)
            vdfs.Dispose();
    }
}
