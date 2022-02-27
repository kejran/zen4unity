using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;

public class ZenEditor : EditorWindow
{
    public enum LoadMode
    {
        World,
        Model,
        Skeleton,
        Skin
    }

    public bool genUseMaterials = true;
    private bool genShow = true;
    private LoadMode genLoadModeOld;
    public LoadMode genLoadMode = LoadMode.World;
    private string[] genAvailableFiles;
    private int genSelectedFileIndex;
    public string genFileFilter = "";
    public string genFileFilterOld = "";
    private string[] genFilteredFiles;
    private Vector2 genScroll = new Vector2();
    public bool genUseG2 = false;

    public string vdfsPath = "";
    public string vdfsVerifiedPath = "";
    public string[] vdfsAllArchives;
    public bool[] vdfsLoadArchiveSelection;
    public bool vdfsUseManualArchives = true;
    private bool vdfsShow = true;
    private bool vdfsShowManualArchives = true;

    public Material matBaseOpaque;
    public Material matBaseTransparent;
    public string matTextureField = "_BaseTex";
    public bool matLoadTextures = true;
    private bool matShow = true;

    private bool worldShow = true;
    public string[] worldFiles = new string[0];
    public bool worldLoadVOBs = true;
    public bool worldNestVOBs = true;

    private bool meshShow = true;
    public bool meshCreatePrefab = true;
    public bool meshOverwritePrefab = true;

    static private Texture2D iconImport;
    private Texture2D iconMaterial;
    private Texture2D iconFolder;
    private Texture2D iconFolderOpened;
    private Texture2D iconUnity;
    private Texture2D iconTerrain;
    private Texture2D iconMesh;
    private Texture2D iconRefresh;
    private Texture2D iconGothic;

    private void OnEnable()
    {
        iconImport =        loadIcon("icons/", "import.png");
        iconRefresh =       loadIcon("icons/", "refresh.png");
        iconFolder =        loadIcon("icons/processed/", "folder icon.asset");
        iconFolderOpened =  loadIcon("icons/processed/", "folderopened icon.asset");
        iconMaterial =      loadIcon("icons/processed/unityengine/", "material icon.asset");
        iconTerrain =       loadIcon("icons/processed/unityengine/", "terrain icon.asset");
        iconMesh =          loadIcon("icons/processed/unityengine/", "mesh icon.asset");
        iconUnity =         loadIcon("icons/processed/unityeditor/", "sceneasset icon.asset");
        iconGothic =        loadIcon("Assets/", "g_icon.png");

        genLoadModeOld = 1 - genLoadMode;
        genFileFilterOld = genFileFilter + "-";
    }

    static Texture2D loadIcon(string iconsubpath, string icon)
    {
        var path = iconsubpath;
        if (EditorGUIUtility.isProSkin)
            path += "d_";
        path += icon;
        return EditorGUIUtility.Load(path) as Texture2D;
    }

    [MenuItem("Gothic/Open Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow(typeof(ZenEditor));

        window.titleContent = new GUIContent("Gothic Importer", iconImport);
    }

    bool pathValid()
    {
        return (vdfsPath == vdfsVerifiedPath) && (vdfsVerifiedPath != "");
    }

    void verifyPath()
    {
        var dir = new DirectoryInfo(vdfsPath);
        if (!dir.Exists)
        {
            EditorUtility.DisplayDialog("Archive error", "Provided path is not a valid folder", "OK");
            vdfsVerifiedPath = "";
            return;
        }
        var files = dir.GetFiles("*.vdf");
        if (files.Length == 0)
        {
            EditorUtility.DisplayDialog("Archive error", "Failed to find .VDF files in the directory", "OK");
            vdfsVerifiedPath = "";
            return;
        }
        vdfsVerifiedPath = vdfsPath;
        reloadArchives();
    }

    bool bigButton(string text)
    {
        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();
        GUILayout.Space(80);
        bool r = GUILayout.Button(text, GUILayout.Height(25));
        GUILayout.Space(80);
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
        return r;
    }

    void reloadArchives()
    {
        if (!pathValid()) {
            vdfsAllArchives = new string[0];
            return;
        } else
        {
            var dir = new DirectoryInfo(vdfsPath);
            vdfsAllArchives = dir.GetFiles("*.vdf").Select(x => x.Name).ToArray();
        }
        vdfsLoadArchiveSelection = new bool[vdfsAllArchives.Length];
    }

    void reloadAvailableFiles()
    {
        var filter = genLoadMode switch
        {
            LoadMode.Model => ".MRM",
            LoadMode.World => ".ZEN",
            LoadMode.Skeleton => ".MDH",
            LoadMode.Skin => ".MDM",
            _ => ""
        };
        using (var imp = new Importer())
        {
            imp.LoadArchives(vdfsPath, getSelectedArchives());
            if (genLoadMode == LoadMode.Skeleton || genLoadMode == LoadMode.Skin)
                genAvailableFiles = imp.AllFiles().Where(x => x.Contains(filter) || x.Contains(".MDL")).ToArray();
            else 
                genAvailableFiles = imp.AllFiles().Where(x => x.Contains(filter)).ToArray();
        }
        reloadFilteredFiles();
    }

    void reloadFilteredFiles()
    {
        genFilteredFiles = genFileFilter.Length > 0 ?
        genAvailableFiles.Where(x => x.Contains(genFileFilter.ToUpper())).ToArray() :
        genAvailableFiles;
    }


    string[] getSelectedArchives()
    {
        if (vdfsUseManualArchives)
            return vdfsAllArchives;
        return vdfsAllArchives.Where((a, i) => vdfsLoadArchiveSelection[i]).ToArray();
    }

    void matView()
    {
        if (genLoadMode == LoadMode.Skeleton)
            return;
        if (!genUseMaterials) GUI.enabled = false;
        var label = new GUIContent("Materials", iconMaterial);
        matShow = EditorGUILayout.Foldout(matShow && genUseMaterials, label, true, EditorStyles.foldoutHeader);
        if (matShow && genUseMaterials)
        {
            ++EditorGUI.indentLevel;
            matBaseOpaque = (Material)EditorGUILayout.ObjectField(
                "Opaque Base", matBaseOpaque, typeof(Material), false);
            matBaseTransparent = (Material)EditorGUILayout.ObjectField(
                "Transparent Base", matBaseTransparent, typeof(Material), false);
            matTextureField = EditorGUILayout.TextField("Material Texture Slot", matTextureField);

            matLoadTextures = EditorGUILayout.Toggle("Load Textures", matLoadTextures);

            if (matBaseOpaque == null || matBaseTransparent == null)
                EditorGUILayout.Separator();
            if (matBaseOpaque == null)
                EditorGUILayout.HelpBox("No base opaque material assigned!", MessageType.Warning);
            if (matBaseTransparent == null)
                EditorGUILayout.HelpBox("No base transparent material assigned!", MessageType.Warning);

            if (!matLoadTextures)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.HelpBox("Loading of textures is disabled. " +
                    "Material will only be assigned the extracted color.",
                    MessageType.Info);
            }
            --EditorGUI.indentLevel;
            EditorGUILayout.Separator();
        }
        GUI.enabled = true;
    }

    void vdfsView()
    {
        var label = new GUIContent("VDFS", iconFolder);
        vdfsShow = EditorGUILayout.Foldout(vdfsShow, label, true, EditorStyles.foldoutHeader);

        if (vdfsShow)
        {
            ++EditorGUI.indentLevel;
            EditorGUILayout.BeginHorizontal();
            vdfsPath = EditorGUILayout.TextField("VDFS Location", vdfsPath);
            if (GUILayout.Button(new GUIContent(iconFolderOpened), EditorStyles.miniButton, GUILayout.Width(30)))
            {
                GUIUtility.keyboardControl = 0;
                vdfsPath = EditorUtility.OpenFolderPanel("Browse to the Gothic/Data folder", vdfsPath, "");
            }
            EditorGUILayout.EndHorizontal();
            if (!pathValid()) {
                EditorGUILayout.HelpBox("The path to Gothic VDFS archives was not verified. "
                    + "Select the path in the folder browser and press Verify.", MessageType.Error);
                if (bigButton("Verify Path"))
                    verifyPath();
                --EditorGUI.indentLevel;
                return;
            }

            vdfsUseManualArchives = EditorGUILayout.Popup(
                "Archive selection", 
                vdfsUseManualArchives ? 1 : 0, new string[] { "Load All", "Load Specified"}
            ) > 0;
            if (vdfsUseManualArchives)
                vdfsShowManualArchives = EditorGUILayout.Foldout(vdfsShowManualArchives, "Loaded archives");
            if (vdfsUseManualArchives && vdfsShowManualArchives) 
            {
                if (vdfsAllArchives == null) reloadArchives();
                ++EditorGUI.indentLevel;
                for (int i = 0; i < vdfsAllArchives.Length; ++i)
                {
                    var t = vdfsAllArchives[i];
                    EditorGUILayout.BeginHorizontal();
                    vdfsLoadArchiveSelection[i] = EditorGUILayout.ToggleLeft(t.Substring(0, t.Length - 4).ToUpper(), vdfsLoadArchiveSelection[i]);
                    EditorGUILayout.EndHorizontal();
                }
                --EditorGUI.indentLevel;
            }
            if (vdfsUseManualArchives && vdfsLoadArchiveSelection.Count(c => c) == 0)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.HelpBox(
                    "At least one archive needs to be selected to perform any data extraction.",
                    MessageType.Error);
            }

            --EditorGUI.indentLevel;
            EditorGUILayout.Separator();
        }
    }
    void worldView()
    {
        if (genLoadMode != LoadMode.World) return;
        var label = new GUIContent("World", iconTerrain);
        worldShow = EditorGUILayout.Foldout(worldShow, label, true, EditorStyles.foldoutHeader);
        if (worldShow)
        {
            ++EditorGUI.indentLevel;
            worldLoadVOBs = EditorGUILayout.Toggle("Load VOBs", worldLoadVOBs);
            EditorGUILayout.Separator();
            EditorGUI.BeginDisabledGroup(!worldLoadVOBs);
            EditorGUILayout.LabelField("VOB settings", EditorStyles.boldLabel);
            ++EditorGUI.indentLevel;
            worldNestVOBs = EditorGUILayout.ToggleLeft("Import Hierarchy", worldNestVOBs);
            worldNestVOBs = EditorGUILayout.ToggleLeft("Import Visuals", worldNestVOBs);
            --EditorGUI.indentLevel;
            EditorGUI.EndDisabledGroup();
            --EditorGUI.indentLevel;
            EditorGUILayout.Separator();
        }
    }

    void meshView()
    {
        bool show = genLoadMode == LoadMode.Model || (genLoadMode == LoadMode.World);
        if (!show) return;

        var label = new GUIContent("Mesh", iconMesh);
        meshShow = EditorGUILayout.Foldout(meshShow, label, true, EditorStyles.foldoutHeader);
        if (meshShow)
        {
            ++EditorGUI.indentLevel;
            EditorGUILayout.LabelField("Prefab settings", EditorStyles.boldLabel);
            ++EditorGUI.indentLevel;
            meshCreatePrefab = EditorGUILayout.ToggleLeft("Create new", meshCreatePrefab);
            meshOverwritePrefab = EditorGUILayout.ToggleLeft("Overwrite existing", meshOverwritePrefab);
            --EditorGUI.indentLevel;
            --EditorGUI.indentLevel;
            EditorGUILayout.Separator();
        }
    }

    void skeletonView() {
        bool show = genLoadMode == LoadMode.Skeleton;
        if (!show) return;
    }

    void generalView()
    {
        var label = new GUIContent("General", iconImport);
        genShow = EditorGUILayout.Foldout(genShow, label, true, EditorStyles.foldoutHeader);
        
        if (genShow)
        {
            ++EditorGUI.indentLevel;
            genLoadMode = (LoadMode)EditorGUILayout.EnumPopup("Import Type", genLoadMode);
            if (genLoadMode != genLoadModeOld) 
                reloadAvailableFiles();
            genLoadModeOld = genLoadMode;
            genUseG2 = EditorGUILayout.Toggle("Gothic 2 mode", genUseG2);
            genUseMaterials = EditorGUILayout.Toggle("Load materials", genUseMaterials);
            --EditorGUI.indentLevel;
            EditorGUILayout.Separator();
        }
    }

    int tab;
    void headerView() {
        var style = new GUIStyle("Toolbar");
        style.fixedHeight = 0;
        style.stretchWidth = true;
        EditorGUILayout.BeginHorizontal(style);

        //tab = GUILayout.Toolbar (tab, new string[] {"Settings", "World", "Mesh"});

        //var old = GUI.backgroundColor;
        //GUI.backgroundColor = new Color(0, 0, 0, 0);
        //GUILayout.Box(iconGothic);
        //GUI.backgroundColor = old;
        //EditorGUILayout.BeginVertical();
        //EditorGUILayout.LabelField("Hello world", EditorStyles.largeLabel);
        //EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    string skinSkeletonFile_ = "";

    void OnGUI()
    {
        headerView();
        genScroll = EditorGUILayout.BeginScrollView(genScroll);
        EditorGUILayout.Separator();
        
        if (pathValid())
            generalView();

        vdfsView();
        
        if (pathValid()) {
            matView();
            worldView();
            meshView();
            skeletonView();
        }

        EditorGUILayout.EndScrollView();

        if (pathValid()) {

            if (genAvailableFiles == null) 
                reloadAvailableFiles();

            var s = new GUIStyle("Toolbar"); 
            s.fixedHeight = 0;
        
            EditorGUILayout.BeginVertical(s);
            genFileFilter = EditorGUILayout.TextField("File Filter", genFileFilter).ToUpper();
            if (genFileFilter != genFileFilterOld)
                reloadFilteredFiles();
            genFileFilterOld = genFileFilter;

            if (genLoadMode == LoadMode.Skin)
                skinSkeletonFile_ = EditorGUILayout.TextField("Skeleton file", skinSkeletonFile_).ToUpper();

            EditorGUILayout.BeginHorizontal();
            genSelectedFileIndex = EditorGUILayout.Popup("File to Import", genSelectedFileIndex, genFilteredFiles);
            if (GUILayout.Button(new GUIContent(iconRefresh), EditorStyles.miniButton, GUILayout.Width(30)))
                reloadAvailableFiles();
            EditorGUILayout.EndHorizontal();

            if (bigButton(genLoadMode switch {
                LoadMode.World => "Load World", 
                LoadMode.Model => "Load Mesh", 
                LoadMode.Skeleton => "Load Skeleton",
                LoadMode.Skin => "Load Skin",
                _ => ""
                //LoadMode.SkeletonSkin => ""
            }))
                runLoad();
            
            EditorGUILayout.EndVertical();
        }
    }

    void runLoad()
    {
        if (genFilteredFiles.Length <= genSelectedFileIndex) return;

        var settings = new Importer.MeshLoadSettings() { 
            loadMaterials = genUseMaterials,
            materialSettings = new Importer.MaterialLoadSettings()
            {
                opaqueMaterialTemplate = matBaseOpaque,
                transparentMaterialTemplate = matBaseTransparent,
                loadTextures = matLoadTextures
            }
        };
        
        using (var imp = new Importer("Assets/Gothic"))
        {
            imp.LoadArchives(vdfsPath, getSelectedArchives());
            if (genLoadMode == LoadMode.World)
            {
                imp.LoadWorld(genFilteredFiles[genSelectedFileIndex], genUseG2);
                imp.ImportWorldMesh(settings);
            }
            if (genLoadMode == LoadMode.Model)
                imp.ImportMesh(genFilteredFiles[genSelectedFileIndex], settings);
            
            if (genLoadMode == LoadMode.Skeleton) {
                imp.ImportSkeleton(genFilteredFiles[genSelectedFileIndex]);
            }
            if (genLoadMode == LoadMode.Skin) {
                imp.ImportSkin(genFilteredFiles[genSelectedFileIndex], skinSkeletonFile_, settings);
            }
        }
    }
}
