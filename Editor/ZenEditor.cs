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
    public bool genUseG2 = false;
    private Vector2 genScroll = new Vector2();

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

    private int fileBrowseOffset = 0;
    private string[] fileAvailableList;
    private string fileSelected = "";
    public string fileFilter = "";
    public string fileFilterOld = "";
    private string[] fileFilteredList;
    private Vector2 fileScroll = new Vector2();

    static private Texture2D iconImport;
    private Texture2D iconMaterial;
    private Texture2D iconFolder;
    private Texture2D iconFolderOpened;
    private Texture2D iconUnity;
    private Texture2D iconTerrain;
    private Texture2D iconAvatar;
    private Texture2D iconSkin;
    private Texture2D iconMesh;
    private Texture2D iconRefresh;
    private Texture2D iconLeft;
    private Texture2D iconRight;
    private Texture2D iconGothic;
  
    private void OnEnable()
    {
        iconImport =        loadIcon("icons/", "import.png");
        iconRefresh =       loadIcon("icons/", "refresh.png");
        iconLeft =          loadIcon("icons/", "tab_prev.png");
        iconRight =         loadIcon("icons/", "tab_next.png");
        iconFolder =        loadIcon("icons/processed/", "folder icon.asset");
        iconFolderOpened =  loadIcon("icons/processed/", "folderopened icon.asset");
        iconMaterial =      loadIcon("icons/processed/unityengine/", "material icon.asset");
        iconTerrain =       loadIcon("icons/processed/unityengine/", "terrain icon.asset");
        iconAvatar =        loadIcon("icons/processed/unityengine/", "avatar icon.asset");
        iconMesh =          loadIcon("icons/processed/unityengine/", "mesh icon.asset");
        iconSkin =          loadIcon("icons/processed/unityengine/", "skinnedmeshrenderer icon.asset");
        iconUnity =         loadIcon("icons/processed/unityeditor/", "sceneasset icon.asset");
        iconGothic =        loadIcon("Assets/", "g_icon.png");

        genLoadModeOld = 1 - genLoadMode;
        fileFilterOld = fileFilter + "-";
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
        fileBrowseOffset = 0;
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
                fileAvailableList = imp.AllFiles().Where(x => x.Contains(filter) || x.Contains(".MDL")).ToArray();
            else 
                fileAvailableList = imp.AllFiles().Where(x => x.Contains(filter)).ToArray();
        }
        reloadFilteredFiles();
    }

    void reloadFilteredFiles()
    {
        fileFilteredList = fileFilter.Length > 0 ?
            fileAvailableList.Where(x => x.Contains(fileFilter.ToUpper())).ToArray() :
            fileAvailableList;
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
        GUILayout.Space(40);
        EditorGUILayout.BeginVertical();
        GUILayout.Space(8);
        tab = GUILayout.Toolbar (tab, new string[] {"Settings", "File", "Script"}, GUILayout.Height(24));
        GUILayout.Space(8);
        EditorGUILayout.EndVertical();
        GUILayout.Space(40);
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

    void settingsView() {
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
    }

    void fileListView() {
        
        var s = new GUIStyle("Toolbar"); 
        s.fixedHeight = 0;
    
        EditorGUILayout.BeginVertical(s);
        fileFilter = EditorGUILayout.TextField("File Filter", fileFilter).ToUpper();
        if (fileFilter != fileFilterOld)
            reloadFilteredFiles();
        fileFilterOld = fileFilter;
        EditorGUILayout.EndVertical();

        fileScroll = EditorGUILayout.BeginScrollView(fileScroll);

        var style = new GUIStyle(EditorStyles.label);
        style.normal.background = Texture2D.whiteTexture;
        style.margin = new RectOffset(0, 0, 0, 0);

        var origColor = GUI.backgroundColor;
        var evenC = new Color(0,0,0,0);
        var oddC = new Color(0,0,0,0.2f);
        var highC = new Color(0.17f, 0.36f, 0.53f, 1.0f);
        int count = 0;
        foreach (var f in fileFilteredList.Skip(fileBrowseOffset * 100).Take(100)) {
            GUI.backgroundColor = f == fileSelected ? highC : (count & 1) == 0 ? evenC : oddC;
            if (GUILayout.Button(f, style))
                fileSelected = f;
            ++count;
        }
        GUI.backgroundColor = origColor;
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent(iconLeft), EditorStyles.miniButtonLeft, GUILayout.Width(50)))
            if (fileBrowseOffset > 0) {
                --fileBrowseOffset;
                fileScroll = Vector2.zero;
            }
        if (GUILayout.Button(new GUIContent(iconRefresh), EditorStyles.miniButtonMid, GUILayout.Width(50)))
            reloadAvailableFiles();
        if (GUILayout.Button(new GUIContent(iconRight), EditorStyles.miniButtonRight, GUILayout.Width(50)))
            if (fileBrowseOffset < (fileFilteredList.Length + 99) / 100 - 1) {
                ++fileBrowseOffset;
                fileScroll = Vector2.zero;
            }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
    }

    void loadFilterView() {
        if (fileAvailableList == null) 
            reloadAvailableFiles();

        var s = new GUIStyle("Toolbar"); 
        s.fixedHeight = 0;
    
        EditorGUILayout.BeginVertical(s);

        if (genLoadMode == LoadMode.Skin)
            skinSkeletonFile_ = EditorGUILayout.TextField("Skeleton file", skinSkeletonFile_).ToUpper();

        
        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        genLoadMode = (LoadMode)GUILayout.Toolbar(
            (int)genLoadMode, new Texture[] { iconTerrain, iconMesh, iconAvatar, iconSkin }, 
            GUILayout.Height(24), GUILayout.Width(32*4));
        
        if (genLoadMode != genLoadModeOld)
        {
            reloadAvailableFiles();
            Repaint();
        }
        genLoadModeOld = genLoadMode;

        GUILayout.Space(24);

        if (GUILayout.Button(genLoadMode switch {
            LoadMode.World => "Load World", 
            LoadMode.Model => "Load Mesh", 
            LoadMode.Skeleton => "Load Skeleton",
            LoadMode.Skin => "Load Skin",
            _ => ""
            //LoadMode.SkeletonSkin => ""
        }, GUILayout.Height(24), GUILayout.Width(32*4)))
            runLoad();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUILayout.EndVertical();
    }

    void OnGUI()
    {
        headerView();

        if (!pathValid())
            tab = 0;

        if (tab == 0)
            settingsView();
        if (tab == 1) {
            fileListView();
            loadFilterView();
        }
    }

    void runLoad()
    {
        if (fileSelected == "") return;

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
                imp.LoadWorld(fileSelected, genUseG2);
                imp.ImportWorldMesh(settings);
            }
            if (genLoadMode == LoadMode.Model)
                imp.ImportMesh(fileSelected, settings);
            
            if (genLoadMode == LoadMode.Skeleton) {
                imp.ImportSkeleton(fileSelected);
            }
            if (genLoadMode == LoadMode.Skin) {
                imp.ImportSkin(fileSelected, skinSkeletonFile_, settings);
            }
        }
    }
}
