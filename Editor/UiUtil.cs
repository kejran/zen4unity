using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class UiUtil 
{
    public Texture2D iconImport;
    public Texture2D iconMaterial;
    public Texture2D iconFolder;
    public Texture2D iconFolderOpened;
    public Texture2D iconUnity;
    public Texture2D iconTerrain;
    public Texture2D iconAvatar;
    public Texture2D iconSkin;
    public Texture2D iconMesh;
    public Texture2D iconAnimation;
    public Texture2D iconMorph;
    public Texture2D iconScript;
    public Texture2D iconRefresh;
    public Texture2D iconLeft;
    public Texture2D iconRight;
    public Texture2D iconGothic;

    static Texture2D loadIcon(string iconsubpath, string icon)
    {
        var path = iconsubpath;
        if (EditorGUIUtility.isProSkin)
            path += "d_";
        path += icon;
        return EditorGUIUtility.Load(path) as Texture2D;
    }

    public UiUtil() {
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
        iconAnimation =     loadIcon("icons/processed/unityengine/", "animationclip icon.asset");
        iconMorph =         loadIcon("icons/", "editcollider.png");
        iconScript =        loadIcon("icons/processed/unityengine/", "scriptableobject icon.asset");
        iconUnity =         loadIcon("icons/processed/unityeditor/", "sceneasset icon.asset");
        iconGothic =        loadIcon("Assets/", "g_icon.png");
    }

    public bool foldout(bool value, string text) {
        EditorGUILayout.BeginVertical(EditorStyles.toolbar);
        var v = EditorGUILayout.Foldout(value, text);
        EditorGUILayout.EndVertical();
        return v;
    }

    public bool bigButton(string text)
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
}
