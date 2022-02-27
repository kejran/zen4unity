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

    public Importer(string assetRoot = "") {
        root = assetRoot;
        vdfs = new VDFS();
    }

    public string[] AllFiles()
    {
        return vdfs.Files();
    }

    public void LoadArchives(string basePath, string[] archives)
    {
        foreach (var f in archives)
            vdfs.LoadArchive(Path.Combine(basePath, f));
        
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
        Directory.CreateDirectory(Path.Combine(root, name));
    }

    private T loadAsset<T>(string path) where T : UnityEngine.Object
    {
        return (T)AssetDatabase.LoadAssetAtPath(path, typeof(T));
    }

    private Texture2D makeTexture(string name, string assetName)
    {
        var path = Path.Combine(root, "Textures", assetName);
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
        var path = Path.Combine(root, "Materials", texture.Replace(".TGA", "") + ".mat");
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

    Material[] makeMaterials(ZMesh zmesh, MaterialLoadSettings settings)
    {
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

    void setTransform(Transform t, Matrix4x4 mat) {
        t.localPosition = new Vector3(mat.m03, mat.m13, mat.m23) * 0.01f;
        t.localRotation = mat.rotation;
        t.localScale = mat.lossyScale;
    }

    Transform[] iterNodes(Transform? parent, ZMeshLib.Node[] nodes) {
        return nodes.Select(n => {
            var go = new GameObject(n.name);
            var t = go.transform;
            if (parent != null)
                t.parent = parent;
            setTransform(t, n.transform);
            iterNodes(t, n.children);
            return t;
        }).ToArray();
    }

    private Transform makeSkeleton(ZMeshLib lib) {
        var nodes = lib.Nodes();
        if (nodes.Length == 0)
            throw new Exception("No root nodes found");
        if (nodes.Length > 1) {
            var s = "Expected 1 root node, got: [" + nodes[0].name;
            foreach (var nn in nodes.Skip(1))
                s += ", " + nn.name;
            throw new Exception(s + "]");
        } 
        var res = iterNodes(null, nodes);
        return res[0];
    }

    private UnityEngine.Object importSkeleton(ZMeshLib lib, string assetName) {
        var path = Path.Combine(root, "Avatars", assetName + ".asset");
        var rootBone = makeSkeleton(lib);
        var go = rootBone.gameObject;
        var avatar = AvatarBuilder.BuildGenericAvatar(go, "");
        makeDir("Avatars");
        AssetDatabase.CreateAsset(avatar, path);

        var ani = go.AddComponent<Animator>();
        ani.avatar = avatar;

        makeDir("Rigs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path.Combine(root, "Rigs", assetName + ".prefab"));
        AssetDatabase.Refresh();
        GameObject.DestroyImmediate(go);
        return PrefabUtility.InstantiatePrefab(prefab);
    }

    private GameObject importMeshImplObj(ZMesh zmesh, MeshLoadSettings settings, string assetName)
    {
        var umesh = makeMesh(zmesh);
        var path = Path.Combine(root, "Meshes", assetName + ".asset");
        makeDir("Meshes");
        AssetDatabase.CreateAsset(umesh, path);

        var go = new GameObject();
        go.name = assetName;
        var mf = go.AddComponent<MeshFilter>();
        mf.mesh = umesh;

        var mr = go.AddComponent<MeshRenderer>();
        
        if (settings.loadMaterials)
        {
            if (settings.materialSettings.loadTextures)
                makeDir("Textures");
            makeDir("Materials");
            mr.materials = makeMaterials(zmesh, settings.materialSettings);
        }
        return go;
    }

    private UnityEngine.Object packageAsPrefab(GameObject go, string assetName)
    {
        makeDir("Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path.Combine(root, "Prefabs", assetName + ".prefab"));
        AssetDatabase.Refresh();
        GameObject.DestroyImmediate(go);
        return prefab;
    }

    private UnityEngine.Object importMeshImpl(ZMesh zmesh, MeshLoadSettings settings, string assetName)
    {
        var go = importMeshImplObj(zmesh, settings, assetName);
        return packageAsPrefab(go, assetName);
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

            if (visual.EndsWith("3DS") || visual.EndsWith("ASC") || visual.EndsWith("MDS"))
            {
                var compressed = visual.Replace(".3DS", ".MRM").Replace(".ASC", ".MDL");
                if (name == "")
                    name = "[" + vob.visual() + "]";

                obj = getOrMakePrefab(compressed, visual.Replace(".3DS", "").Replace(".ASC", ""), settings);
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

    private GameObject getOrMakePrefab(string visual, string assetName, MeshLoadSettings settings)
    {
        var path = Path.Combine(root, "Prefabs", assetName + ".prefab");
        var prefab = loadAsset<UnityEngine.Object>(path);
        if (prefab == null)
        {
            if (visual.EndsWith("MRM"))
                using (var zmesh = new ZMesh(vdfs, visual))
                    prefab = importMeshImpl(zmesh, settings, assetName);

            if (visual.EndsWith("MDL"))
            {
                // Debug.Log(visual);
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
                    void iterateNodes(GameObject parent, ZMeshLib.Node[] nodes)
                    {
                        foreach (var node in nodes) {
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
                prefab = packageAsPrefab(go, assetName);
            }

        }
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    public void ImportWorldMesh(MeshLoadSettings settings)
    {
        using (var zmesh = zen!.mesh())
            PrefabUtility.InstantiatePrefab(importMeshImpl(zmesh, settings, zen.Name));

        importVOBs(zen.data().vobs(), settings);
    }

    public void ImportMesh(string name, MeshLoadSettings settings)
    {
        using (var zmesh = new ZMesh(vdfs, name))
            PrefabUtility.InstantiatePrefab(importMeshImpl(zmesh, settings, name));
    }

    public void ImportSkeleton(string name) {
        using (var lib = new ZMeshLib(vdfs, name))
            importSkeleton(lib, name);
    }

    public void Dispose()
    {
        if (zen != null)
            zen.Dispose();
        if (vdfs != null) 
            vdfs.Dispose();
    }
}
