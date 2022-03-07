using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;

public class ImportWindow : EditorWindow
{
	public enum LoadMode
	{
		World,
		Model,
		Skeleton,
		Skin,
		Animation,
		Morph,
		Script
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
	public bool worldLoadMesh = true;
	public bool worldLoadWaynet = true;
	public bool worldLoadVOBs = true;
	//public bool worldNestVOBs = true;
	public bool worldLoadStatic = true;
	public bool worldLoadStructural = true;
	public bool worldLoadScripts = true;

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
	private string fileSkeleton = "";

	Importer.ScriptData scriptData;
	Vector2 scriptScroll = Vector2.zero;
	bool scriptImportSkeleton;
	bool scriptImportTree;
	bool[] scriptImportMeshes;
	bool[] scriptImportAnis;

	bool toolMakeNpc = true;
	int npcSkin = 0;
	string npcHead = "";
	int npcHeadId = 0;
	string npcBody = "";
	int npcBodyId = 0;
	string npcArmor = "";
	string npcArmorTex = "";

	UiUtil ui;

	private void OnEnable()
	{
		ui = new UiUtil();
		genLoadModeOld = 1 - genLoadMode;
		fileFilterOld = fileFilter + "-";

		titleContent = new GUIContent("Gothic Importer", ui.iconImport);
		loadModeTextures = new Texture[] { 
			ui.iconTerrain, ui.iconMesh, 
			ui.iconAvatar, ui.iconSkin, 
			ui.iconAnimation, ui.iconMorph, 
			ui.iconScript 
		};
	}

	[MenuItem("zen4unity/Importer")]
	public static void ShowWindow()
	{
		var window = GetWindow(typeof(ImportWindow));
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
		var filter1 = genLoadMode switch
		{
			LoadMode.Model => ".MRM",
			LoadMode.World => ".ZEN",
			LoadMode.Skeleton => ".MDH",
			LoadMode.Skin => ".MDM",
			LoadMode.Animation => ".MAN",
			LoadMode.Morph => ".MMB",
			LoadMode.Script => ".MDS",
			_ => "$$$$$$$$$$$$"
		};
		var filter2 = genLoadMode switch
		{
			LoadMode.Skeleton => ".MDL",
			LoadMode.Skin => ".MDL",
			LoadMode.Script=> ".MSB",
			_ => "$$$$$$$$$$$$"
		};

		using (var imp = new Importer())
		{
			imp.LoadArchives(vdfsPath, getSelectedArchives());
			fileAvailableList = imp.AllFiles().Where(x => x.Contains(filter1) || x.Contains(filter2)).ToArray();
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
		if (!vdfsUseManualArchives)
			return vdfsAllArchives;
		return vdfsAllArchives.Where((a, i) => vdfsLoadArchiveSelection[i]).ToArray();
	}

	void matView()
	{
		if (genLoadMode == LoadMode.Skeleton)
			return;
		if (!genUseMaterials) GUI.enabled = false;
		var label = new GUIContent("Materials", ui.iconMaterial);
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
		var label = new GUIContent("VDFS", ui.iconFolder);
		vdfsShow = EditorGUILayout.Foldout(vdfsShow, label, true, EditorStyles.foldoutHeader);

		if (vdfsShow)
		{
			++EditorGUI.indentLevel;
			EditorGUILayout.BeginHorizontal();
			vdfsPath = EditorGUILayout.TextField("VDFS Location", vdfsPath);
			if (GUILayout.Button(new GUIContent(ui.iconFolderOpened), EditorStyles.miniButton, GUILayout.Width(30)))
			{
				GUIUtility.keyboardControl = 0;
				vdfsPath = EditorUtility.OpenFolderPanel("Browse to the Gothic/Data folder", vdfsPath, "");
			}
			EditorGUILayout.EndHorizontal();
			if (!pathValid()) {
				EditorGUILayout.HelpBox("The path to Gothic VDFS archives was not verified. "
					+ "Select the path in the folder browser and press Verify.", MessageType.Error);
				if (ui.bigButton("Verify Path"))
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
		var label = new GUIContent("World", ui.iconTerrain);
		worldShow = EditorGUILayout.Foldout(worldShow, label, true, EditorStyles.foldoutHeader);
		if (worldShow)
		{
			++EditorGUI.indentLevel;
			worldLoadMesh = EditorGUILayout.Toggle("Load Mesh", worldLoadMesh);
			worldLoadWaynet = EditorGUILayout.Toggle("Load Waynet", worldLoadWaynet);
			worldLoadVOBs = EditorGUILayout.Toggle("Load VOBs", worldLoadVOBs);
			EditorGUILayout.Separator();
			EditorGUI.BeginDisabledGroup(!worldLoadVOBs);
			EditorGUILayout.LabelField("VOB settings", EditorStyles.boldLabel);
			++EditorGUI.indentLevel;
			//worldNestVOBs = EditorGUILayout.ToggleLeft("Import Hierarchy", worldNestVOBs);
			worldLoadStatic = EditorGUILayout.ToggleLeft("Import Static", worldLoadStatic);
			worldLoadStructural = EditorGUILayout.ToggleLeft("Import Structural", worldLoadStructural);
			worldLoadScripts = EditorGUILayout.ToggleLeft("Import Scripts", worldLoadScripts);

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

		var label = new GUIContent("Mesh", ui.iconMesh);
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
		var label = new GUIContent("General", ui.iconImport);
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
	string[] headerStrings = new string[] {"Settings", "File", "Script", "Tools"};
	void headerView() {
		var style = new GUIStyle("Toolbar");
		style.fixedHeight = 0;
		style.stretchWidth = true;
		EditorGUILayout.BeginHorizontal(style);
		GUILayout.Space(40);
		EditorGUILayout.BeginVertical();
		GUILayout.Space(8);
		int prevTab = tab;
		tab = GUILayout.Toolbar (tab, headerStrings, GUILayout.Height(24));
		if (prevTab != tab)
			GUI.FocusControl(null);
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
		if (GUILayout.Button(new GUIContent(ui.iconLeft), EditorStyles.miniButtonLeft, GUILayout.Width(50)))
			if (fileBrowseOffset > 0) {
				--fileBrowseOffset;
				fileScroll = Vector2.zero;
			}
		if (GUILayout.Button(new GUIContent(ui.iconRefresh), EditorStyles.miniButtonMid, GUILayout.Width(50)))
			reloadAvailableFiles();
		if (GUILayout.Button(new GUIContent(ui.iconRight), EditorStyles.miniButtonRight, GUILayout.Width(50)))
			if (fileBrowseOffset < (fileFilteredList.Length + 99) / 100 - 1) {
				++fileBrowseOffset;
				fileScroll = Vector2.zero;
			}
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space(8);
	}

	Texture[] loadModeTextures;
	void loadFilterView() {
		if (fileAvailableList == null)
			reloadAvailableFiles();

		var s = new GUIStyle("Toolbar");
		s.fixedHeight = 0;

		EditorGUILayout.BeginVertical(s);

		if (genLoadMode == LoadMode.Skin || genLoadMode == LoadMode.Animation)
			fileSkeleton = EditorGUILayout.TextField("Skeleton file", fileSkeleton).ToUpper();

		bool barVertical = false;
		var barWidth = 32 * 7;
		var btnWidth = 32 * 4;

		if (EditorGUIUtility.currentViewWidth < barWidth + btnWidth + 32*2) {
			barVertical = true;
			btnWidth = barWidth;
		}

		EditorGUILayout.Space();
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();

		if (barVertical)
			GUILayout.BeginVertical();

		genLoadMode = (LoadMode)GUILayout.Toolbar(
			(int)genLoadMode, loadModeTextures,
			GUILayout.Height(24), GUILayout.Width(barWidth));

		if (genLoadMode != genLoadModeOld)
		{
			reloadAvailableFiles();
			Repaint();
		}
		genLoadModeOld = genLoadMode;

		GUILayout.Space(barVertical ? 4 : 24);

		if (GUILayout.Button(genLoadMode switch {
			LoadMode.World => "Load World",
			LoadMode.Model => "Load Mesh",
			LoadMode.Skeleton => "Load Skeleton",
			LoadMode.Skin => "Load Skin",
			LoadMode.Animation => "Load Animation",
			LoadMode.Morph => "Load Morph",
			LoadMode.Script => "Load Script",
			_ => ""
		}, GUILayout.Height(24), GUILayout.Width(btnWidth)))
			runLoad();

		if (barVertical)
			EditorGUILayout.EndVertical();

		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		EditorGUILayout.Space();

		EditorGUILayout.EndVertical();
	}

	public Importer.MeshLoadSettings makeMeshSettings() {
		return new Importer.MeshLoadSettings() {
			loadMaterials = genUseMaterials,
			materialSettings = new Importer.MaterialLoadSettings()
			{
				opaqueMaterialTemplate = matBaseOpaque,
				transparentMaterialTemplate = matBaseTransparent,
				loadTextures = matLoadTextures
			}
		};
	}

	void listSelectBtn(bool[] values) {
		EditorGUILayout.Separator();
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		if (GUILayout.Button("None", EditorStyles.miniButtonLeft, GUILayout.Width(48)))
			for (int i = 0; i < values.Length; ++i)
				values[i] = false;
		if (GUILayout.Button("All", EditorStyles.miniButtonRight, GUILayout.Width(48)))
			for (int i = 0; i < values.Length; ++i)
				values[i] = true;
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Separator();
	}

	void scriptView() {
		if (scriptData == null)
			return;

		scriptScroll = EditorGUILayout.BeginScrollView(scriptScroll);

		ui.foldout(true, "Hierarchy");
		++EditorGUI.indentLevel;
		scriptImportSkeleton = EditorGUILayout.ToggleLeft(scriptData.hierarchy, scriptImportSkeleton);
		--EditorGUI.indentLevel;
		EditorGUILayout.Separator();

		if (scriptData.baseMesh != "") {
			ui.foldout(true, "Base mesh");
			++EditorGUI.indentLevel;
			scriptImportTree = EditorGUILayout.ToggleLeft(scriptData.baseMesh, scriptImportTree);
			--EditorGUI.indentLevel;
			EditorGUILayout.Separator();
		}

		ui.foldout(true, "Registered meshes");
		++EditorGUI.indentLevel;
		var c = GUI.backgroundColor;
		for (int i = 0; i < scriptData.registeredMeshes.Length; ++i)
			scriptImportMeshes[i] = EditorGUILayout.ToggleLeft(
				scriptData.registeredMeshes[i], scriptImportMeshes[i]);

		--EditorGUI.indentLevel;
		listSelectBtn(scriptImportMeshes);

		ui.foldout(true, "Animations");
		++EditorGUI.indentLevel;
		for (int i = 0; i < scriptData.anims.Length; ++i)
			scriptImportAnis[i] = EditorGUILayout.ToggleLeft(
				scriptData.anims[i], scriptImportAnis[i]);

		--EditorGUI.indentLevel;
		listSelectBtn(scriptImportAnis);

		EditorGUILayout.EndScrollView();

		if (ui.bigButton("Load Files"))
			using (var importer = new Importer("Assets/Gothic"))
			{
				importer.LoadArchives(vdfsPath, getSelectedArchives());
				var skeleton = scriptData.hierarchy + ".MDH";
				var settings = makeMeshSettings();

				void tryImportSkin(string skin) {
					var path = importer.findSkin(skin);
					if (path != "")
						importer.ImportSkinOrDynamic(path, skeleton, settings);
					else
						Debug.LogWarningFormat("Failed to find skin [{0}]", skin);
				}

				if (scriptImportSkeleton)
					importer.ImportSkeleton(skeleton);
				if (scriptImportTree && scriptData.baseMesh != "")
					tryImportSkin(scriptData.baseMesh);
				for (int i = 0; i < scriptImportMeshes.Length; ++i)
					if (scriptImportMeshes[i])
						tryImportSkin(scriptData.registeredMeshes[i]);
				for (int i = 0; i < scriptImportAnis.Length; ++i)
					if (scriptImportAnis[i])
						importer.ImportAnimation(scriptData.hierarchy + "-" + scriptData.anims[i] + ".MAN", skeleton);
			}
	}
		
	string prepPath(string path, string ext) 
	{
		return System.IO.Path.GetFileNameWithoutExtension(path) + "." + ext;
	}

	void makeHuman() 
	{
		toolMakeNpc = ui.foldout(toolMakeNpc, "Make NPC");
		if (toolMakeNpc) 
		{
			npcSkin = EditorGUILayout.IntField("Skin color", npcSkin);
			npcHead = EditorGUILayout.TextField("Head mesh", npcHead).ToUpper();
			npcHeadId = EditorGUILayout.IntField("Head texture", npcHeadId);
			npcBody = EditorGUILayout.TextField("Body mesh", npcBody).ToUpper();
			npcBodyId = EditorGUILayout.IntField("Body texture", npcBodyId);
			npcArmor = EditorGUILayout.TextField("Armor mesh", npcArmor).ToUpper();
			npcArmorTex = EditorGUILayout.TextField("Armor texture", npcArmorTex).ToUpper();

			if (ui.bigButton("Create NPC")) 
				using (var importer = new Importer("Assets/Gothic")) 
				{
					importer.LoadArchives(vdfsPath, getSelectedArchives());
					var window = GetWindow(typeof(ImportWindow)) as ImportWindow;
					var settings = window.makeMeshSettings();
					var go = importer.ImportSkin(prepPath(npcBody, "MDM"), "HUMANS.MDH", settings);
					var skin = go.GetComponent<SkinnedMeshRenderer>();
					var transforms = skin.bones;
					var bodyTex = "HUM_BODY_NAKED_V" + npcBodyId + "_C" + npcSkin + ".TGA";
						skin.material = importer.MakeMaterial(bodyTex, 
						settings.materialSettings);
 
					var headBone = skin.bones[6];//"BIP01 HEAD"
 
					var head = importer.ImportMorph(prepPath(npcHead, "MMB"), settings);
					var headTex = "HUM_HEAD_V" + npcHeadId + "_C" + npcSkin + ".TGA";
					head.GetComponent<SkinnedMeshRenderer>().material = 
						importer.MakeMaterial(headTex, 
						settings.materialSettings);
					head.transform.parent = headBone;
					head.transform.localPosition = Vector3.zero;
					head.transform.localRotation = Quaternion.identity;
				}
		}
	}


	void toolView() {
		makeHuman();
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
		if (tab == 2)
			scriptView();

		if (tab == 3)
			toolView();
	}

	void runLoad()
	{
		if (fileSelected == "") return;

		var settings = makeMeshSettings();

		using (var imp = new Importer("Assets/Gothic"))
		{
			imp.LoadArchives(vdfsPath, getSelectedArchives());
			
			switch (genLoadMode) {
			case LoadMode.World:
				imp.LoadWorld(fileSelected, genUseG2);
				if (worldLoadMesh)
					imp.ImportWorldMesh(settings);
				if (worldLoadVOBs)
				{
					var pSettings = new Importer.PrefabLoadSettings();
					pSettings.loadStatic = worldLoadStatic;
					pSettings.loadStructural = worldLoadStructural;
					pSettings.loadScripts = worldLoadScripts;
					imp.ImportVobs(settings, pSettings);
				}
				if (worldLoadWaynet)
					imp.ImportWaynet();
				break;

			case LoadMode.Model:
				imp.ImportMesh(fileSelected, settings);
				break;

			case LoadMode.Skeleton:
				imp.ImportSkeleton(fileSelected);
				break;

			case LoadMode.Skin:
				imp.ImportSkinOrDynamic(fileSelected, fileSkeleton, settings);
				break;

			case LoadMode.Animation:
				imp.ImportAnimation(fileSelected, fileSkeleton);
				break;

			case LoadMode.Morph:
				imp.ImportMorph(fileSelected, settings);
				break;

			case LoadMode.Script:
				scriptData = imp.ImportScript(fileSelected);
				scriptImportTree = true;
				scriptImportSkeleton = false; // any mesh implies it will import anyway
				scriptImportMeshes = scriptData.registeredMeshes.Select(x => false).ToArray();
				scriptImportAnis = scriptData.anims.Select(x => false).ToArray();
				tab = 2;
				break;
			}
		}
	}
}
