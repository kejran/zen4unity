using System;
using System.Collections.Generic;
//using System.IO;
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
        DynamicMesh
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
        if (settings.loadTextures && texture.Contains(".TGA"))
        {
            var compressed = texture.Replace(".TGA", "-C.TEX");
            tex2d = tryGetTexture(compressed);
        }

        var useTransparent = settings.loadTextures && tex2d != null && tex2d.format == TextureFormat.DXT5;
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
                t.parent = parent;
            setTransform(t, n.transform);
            all[n.index] = t;
            iterNodes(t, all, n.children);
            return t;
        }).ToArray();
    }

    private Tuple<Transform, Transform[]> makeSkeleton(ZMeshLib lib) {
        var nodeInfo = lib.Nodes();
        var nodes = nodeInfo.asTree;
        var arr = new Transform[nodeInfo.asArray.Length];
        if (nodes.Length == 0)
            throw new Exception("No root nodes found");
        if (nodes.Length > 1) {
            var s = "Expected 1 root node, got: [" + nodes[0].name;
            foreach (var nn in nodes.Skip(1))
                s += ", " + nn.name;
            throw new Exception(s + "]");
        } 
        var res = iterNodes(null, arr, nodes);
        return Tuple.Create(res[0], arr);
    }

    private GameObject importSkeleton(ZMeshLib lib, string assetName) 
    {
        var path = pathJoin(root, "Avatars", assetName + ".asset");
        var (rootBone, allBones) = makeSkeleton(lib);
        var go = rootBone.gameObject;
        var avatar = AvatarBuilder.BuildGenericAvatar(go, "");
        makeDir("Avatars");
        AssetDatabase.CreateAsset(avatar, path);

        var ani = go.AddComponent<Animator>();
        ani.avatar = avatar;

        var rend = go.AddComponent<SkinnedMeshRenderer>();
        rend.bones = allBones;

        return packageAsPrefab(go, "Rigs", assetName);
    }

    private GameObject importSkin(ZMeshLib lib, string skeleton, string assetName, MeshLoadSettings settings) 
    {
        var path = pathJoin(root, "Skins", assetName + ".asset");
        makeDir("Skins");
        //.Replace(".MDM", ".MDH")
        var go = getOrMakePrefab(skeleton, skeleton, PrefabType.Skeleton, settings);

        var rend = go.GetComponent<SkinnedMeshRenderer>();
        
        using(var zmesh = lib.SkinnedMesh()) {
            var mesh = makeSkinMesh(zmesh, rend.bones);
            AssetDatabase.CreateAsset(mesh, path);
            rend.sharedMesh = mesh;

            if (settings.loadMaterials)
                rend.materials = makeMaterials(zmesh, settings.materialSettings);
        }

        return packageAsPrefab(go, "Prefabs/Skins", assetName);
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

    private void importAnimation(ZAni zani, string skeleton, string assetName) 
    {
        var sp = assetName.Split('-', 2);
        var path = pathJoin(root, "Animations", sp[0], sp[1].Replace(".MAN", "") + ".asset");
        makeDir("Animations/" + sp[0]);

        var go = getOrMakePrefab(skeleton, skeleton, PrefabType.Skeleton, new MeshLoadSettings());
        var ani = new AnimationClip();

        var nodes = zani.nodeIndices();
        var frames = zani.frames();
        var samples = zani.packedSamples();
        float invFps = 1.0f / zani.fps();

        var nextAni_ = zani.next().ToUpper();
        var nextAniName = nextAni_.Length > 0 ? System.IO.Path.GetFileNameWithoutExtension(skeleton) + 
            "-" + nextAni_ + ".MAN" : nextAni_;
        
        // todo add looping/next dummy frame at the end

        if (frames == 0)
        {
            Debug.LogWarning(assetName + " is empty");
            return;
        }

        var skin = go.GetComponent<SkinnedMeshRenderer>();
        
        for (uint node = 0; node < nodes.Length; ++node) {
            var t = skin.bones[nodes[node]];
            var rel = AnimationUtility.CalculateTransformPath(t, go.transform);
            
            var curves = new AnimationCurve[7];
            for (int i = 0; i < 7; ++i)
                curves[i] = new AnimationCurve();

            var translation0 = samples[node].position;
            bool encodeTranslation = false;
            
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
                ani.SetCurve(rel, typeof(Transform), "localPosition.x", curves[0]);
                ani.SetCurve(rel, typeof(Transform), "localPosition.y", curves[1]);
                ani.SetCurve(rel, typeof(Transform), "localPosition.z", curves[2]);
            } else 
            {
                float end = invFps * (frames - 1);
                ani.SetCurve(rel, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0, end, translation0.x));
                ani.SetCurve(rel, typeof(Transform), "localPosition.y", AnimationCurve.Constant(0, end, translation0.y));
                ani.SetCurve(rel, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0, end, translation0.z));
            }
            
            ani.SetCurve(rel, typeof(Transform), "localRotation.x", curves[3]);
            ani.SetCurve(rel, typeof(Transform), "localRotation.y", curves[4]);
            ani.SetCurve(rel, typeof(Transform), "localRotation.z", curves[5]);
            ani.SetCurve(rel, typeof(Transform), "localRotation.w", curves[6]);
        }

        ani.EnsureQuaternionContinuity();
        
        AssetDatabase.CreateAsset(ani, path);

        GameObject.DestroyImmediate(go);

        //return packageAsPrefab(go, "Prefabs/Skins", assetName);
    }

    private GameObject importMeshImplObj(ZMesh zmesh, MeshLoadSettings settings, string assetName)
    {
        var umesh = makeMesh(zmesh);
        var path = pathJoin(root, "Meshes", assetName + ".asset");
        makeDir("Meshes");
        AssetDatabase.CreateAsset(umesh, path);

        var go = new GameObject();
        go.name = assetName;
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

    private GameObject importMeshImpl(ZMesh zmesh, MeshLoadSettings settings, string assetName)
    {
        var go = importMeshImplObj(zmesh, settings, assetName);
        return packageAsPrefab(go, "Models", assetName);
    }

    private GameObject[] importVOBs(VOB[] vobs, MeshLoadSettings settings)
    {
        var result = new List<GameObject>();
        foreach (var vob in vobs)
        {
            var children = importVOBs(vob.children(), settings);

            GameObject? obj = null;
            var name = vob.name();

            var visual = vob.visual().ToUpper();

            // if (vob.type() == VOB.Type.MobContainer)
            //     Debug.Log("CONTAINTER " + visual);

            if (visual.EndsWith("3DS"))// || visual.EndsWith("ASC") || visual.EndsWith("MDS"))
            {
                var compressed = visual.Replace(".3DS", ".MRM");//.Replace(".ASC", ".MDL");
                if (name == "")
                    name = "[" + vob.visual() + "]";
                //.Replace(".ASC", "")
                obj = getOrMakePrefab(compressed, visual.Replace(".3DS", ""), PrefabType.StaticMesh, settings);
            }

            if (obj == null && children.Length > 0)
                obj = new GameObject();

            if (obj != null)
            {
                obj.transform.position = vob.position();
                obj.transform.rotation = vob.rotation();
                result.Add(obj);
            }

            if (obj != null && children.Length > 0)
                foreach (var child in children)
                    child.transform.parent = obj.transform;

        }
        return result.ToArray();
    }

    private GameObject getOrMakePrefab(string visual, string assetName, PrefabType type, MeshLoadSettings settings)
    {   
        var path = pathJoin(root, "Prefabs", assetName + ".prefab");
        var prefab = loadAsset<UnityEngine.Object>(path);
        if (prefab == null)
        {
            if (type == PrefabType.StaticMesh) // MRM
                using (var zmesh = new ZMesh(vdfs, visual))
                    prefab = importMeshImpl(zmesh, settings, assetName);

            if (type == PrefabType.Skeleton) {
                using (var lib = new ZMeshLib(vdfs, visual)) { // MDl, MDH
                    prefab = importSkeleton(lib, assetName);
                }
            }

            if (visual.EndsWith("MDL-dummy-will-fix-later-stop-complaining-aboud-dead-code"))
            {
                var go = new GameObject(assetName);
                using (var lib = new ZMeshLib(vdfs, visual))
                {
                    var attached = lib.Attached();

                    var dict = new Dictionary<string, GameObject>();
                    foreach (var tpl in attached)
                    {
                        // Debug.Log(assetName + "@" + tpl.Item1);
                        var child = importMeshImplObj(tpl.Item2, settings, assetName + "@" + tpl.Item1);
                        tpl.Item2.Dispose();
                        child.name = tpl.Item1;
                        child.transform.parent = go.transform;
                        dict.Add(tpl.Item1, child);
                    }
                    void iterateNodes(GameObject parent, ZMeshLib.NodeInfo nodes)
                    {
                        foreach (var node in nodes.asTree) {
                            GameObject go;
                            if (node.name != "" && dict.ContainsKey(node.name))
                                go = dict[node.name];
                            else
                                go = new GameObject();
                            go.transform.parent = parent.transform;
                            go.name = node.name == "" ? "UNNAMED" : node.name;
                            go.transform.localPosition = node.transform.MultiplyPoint(Vector3.zero);
                            go.transform.localRotation = node.transform.rotation;
                        }
                    }
                    iterateNodes(go, lib.Nodes());
                }
                prefab = packageAsPrefab(go, "TODO", assetName);
            }

        }
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    public void ImportWorldMesh(MeshLoadSettings settings)
    {
        using (var zmesh = zen!.mesh())
            PrefabUtility.InstantiatePrefab(importMeshImpl(zmesh, settings, zen.Name));

    }

    public void ImportVobs(MeshLoadSettings settings) {
        importVOBs(zen!.data().vobs(), settings);
    }

    public void ImportWaynet() {
        var go = new GameObject();
        var r = go.AddComponent<WaynetRenderer>();
        r.waynet = zen!.data().waynet();
    }

    public void ImportMesh(string name, MeshLoadSettings settings)
    {
        using (var zmesh = new ZMesh(vdfs, name))
            PrefabUtility.InstantiatePrefab(importMeshImpl(zmesh, settings, name));
    }

    public void ImportSkeleton(string name) 
    {
        using (var lib = new ZMeshLib(vdfs, name))
            PrefabUtility.InstantiatePrefab(importSkeleton(lib, name));
    }

    public string findSkin(string name) {
        var n = System.IO.Path.GetFileNameWithoutExtension(name);
        var mdl = n + ".MDL";
        var mdm = n + ".MDM";
        if (vdfs.Exists(mdl))
            return mdl;
        if (vdfs.Exists(mdm))
            return mdm;
        return "";    
    }

    public void ImportSkin(string name, string skeletonAsset, MeshLoadSettings settings) 
    {
        using (var lib = new ZMeshLib(vdfs, name))
            PrefabUtility.InstantiatePrefab(importSkin(lib, skeletonAsset, name, settings));
    }

    public void ImportAnimation(string name, string skeletonAsset) 
    {
        using (var lib = new ZAni(vdfs, name))
            /*PrefabUtility.InstantiatePrefab(*/importAnimation(lib, skeletonAsset, name);//);
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
            result.hierarchy = System.IO.Path.GetFileNameWithoutExtension(name.ToUpper()); // MDH
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
