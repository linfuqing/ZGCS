//Assets/Editor/SearchForComponents.cs
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System.Collections.Generic;

namespace ZG
{
    public class SearchForComponents : EditorWindow
    {
        [MenuItem("Window/ZG/Search For Components")]
        static void Init()
        {
            SearchForComponents window = (SearchForComponents)EditorWindow.GetWindow(typeof(SearchForComponents));
            window.Show();
            window.position = new Rect(20, 80, 550, 500);
        }

        string[] modes = new string[] { "Search for component usage", "Search for missing components" };
        string[] checkType = new string[] { "Check single component", "Check all components" };

        ReorderableList listResult;
        List<ComponentNames> prefabComponents, notUsedComponents, addedComponents, existingComponents, sceneComponents;
        int editorMode, selectedCheckType;
        MonoScript targetComponent;
        string componentName = "";

        bool showPrefabs, showAdded, showScene, showUnused = true;
        Vector2 scroll, scroll1, scroll2, scroll3, scroll4;

        class ComponentNames
        {
            public string componentName;
            public string namespaceName;
            public string assetPath;
            public List<string> usageSource;
            public ComponentNames(string comp, string space, string path)
            {
                this.componentName = comp;
                this.namespaceName = space;
                this.assetPath = path;
                this.usageSource = new List<string>();
            }
            public override bool Equals(object obj)
            {
                return ((ComponentNames)obj).componentName == componentName && ((ComponentNames)obj).namespaceName == namespaceName;
            }
            public override int GetHashCode()
            {
                return componentName.GetHashCode() + namespaceName.GetHashCode();
            }
        }

        void OnGUI()
        {
            GUILayout.Label(position + "");
            GUILayout.Space(3);
            int oldValue = GUI.skin.window.padding.bottom;
            GUI.skin.window.padding.bottom = -20;
            Rect windowRect = GUILayoutUtility.GetRect(1, 17);
            windowRect.x += 4;
            windowRect.width -= 7;
            editorMode = GUI.SelectionGrid(windowRect, editorMode, modes, 2, "Window");
            GUI.skin.window.padding.bottom = oldValue;

            switch (editorMode)
            {
                case 0:
                    selectedCheckType = GUILayout.SelectionGrid(selectedCheckType, checkType, 2, "Toggle");
                    GUI.enabled = selectedCheckType == 0;
                    targetComponent = (MonoScript)EditorGUILayout.ObjectField(targetComponent, typeof(MonoScript), false);
                    GUI.enabled = true;

                    if (GUILayout.Button("Check component usage"))
                    {
                        AssetDatabase.SaveAssets();
                        switch (selectedCheckType)
                        {
                            case 0:
                                componentName = targetComponent.name;
                                string[] allPrefabs = GetAllPrefabs();
                                string prefab;//, targetPath = AssetDatabase.GetAssetPath(targetComponent);
                                GameObject gameObject;
                                Component component;
                                System.Type type = targetComponent.GetClass(); 
                                int numAllPrefabs = allPrefabs == null ? 0 : allPrefabs.Length;
                                List <string> result = new List<string>();
                                for(int i = 0; i < numAllPrefabs; ++i)
                                {
                                    prefab = allPrefabs[i];

                                    if (EditorUtility.DisplayCancelableProgressBar("Searching..", prefab, i * 1.0f / numAllPrefabs))
                                        break;

                                    gameObject = AssetDatabase.LoadMainAssetAtPath(prefab) as GameObject;
                                    component = gameObject == null ? null : gameObject.GetComponentInChildren(type);
                                    if (component != null && component.GetType() == type)
                                        result.Add(prefab);

                                    /*string[] single = new string[] { prefab };
                                    string[] dependencies = AssetDatabase.GetDependencies(single);
                                    foreach (string dependedAsset in dependencies)
                                    {
                                        if (dependedAsset == targetPath)
                                        {
                                            result.Add(prefab);
                                        }
                                    }*/
                                }

                                if (listResult != null)
                                    listResult.list = result;

                                EditorUtility.ClearProgressBar();
                                break;
                            case 1:
                                List<string> scenesToLoad = new List<string>();
                                existingComponents = new List<ComponentNames>();
                                prefabComponents = new List<ComponentNames>();
                                notUsedComponents = new List<ComponentNames>();
                                addedComponents = new List<ComponentNames>();
                                sceneComponents = new List<ComponentNames>();

                                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                {
                                    string projectPath = Application.dataPath, asset;
                                    projectPath = projectPath.Substring(0, projectPath.IndexOf("Assets"));

                                    string[] allAssets = AssetDatabase.GetAllAssetPaths();
                                    int numAllAssets = allAssets == null ? 0 : allAssets.Length;
                                    for (int i = 0; i < numAllAssets; ++i)
                                    {
                                        asset = allAssets[i];

                                        if (EditorUtility.DisplayCancelableProgressBar("Searching..", asset, i * 1.0f / numAllAssets))
                                            break;

                                        int indexCS = asset.IndexOf(".cs");
                                        int indexJS = asset.IndexOf(".js");
                                        if (indexCS != -1 || indexJS != -1)
                                        {
                                            ComponentNames newComponent = new ComponentNames(NameFromPath(asset), "", asset);
                                            try
                                            {
                                                System.IO.FileStream FS = new System.IO.FileStream(projectPath + asset, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                                                System.IO.StreamReader SR = new System.IO.StreamReader(FS);
                                                string line;
                                                while (!SR.EndOfStream)
                                                {
                                                    line = SR.ReadLine();
                                                    int index1 = line.IndexOf("namespace");
                                                    int index2 = line.IndexOf("{");
                                                    if (index1 != -1 && index2 != -1)
                                                    {
                                                        line = line.Substring(index1 + 9);
                                                        index2 = line.IndexOf("{");
                                                        line = line.Substring(0, index2);
                                                        line = line.Replace(" ", "");
                                                        newComponent.namespaceName = line;
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                            }

                                            existingComponents.Add(newComponent);

                                            try
                                            {
                                                System.IO.FileStream FS = new System.IO.FileStream(projectPath + asset, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                                                System.IO.StreamReader SR = new System.IO.StreamReader(FS);

                                                string line;
                                                int lineNum = 0;
                                                while (!SR.EndOfStream)
                                                {
                                                    lineNum++;
                                                    line = SR.ReadLine();
                                                    int index = line.IndexOf("AddComponent");
                                                    if (index != -1)
                                                    {
                                                        line = line.Substring(index + 12);
                                                        if (line[0] == '(')
                                                        {
                                                            line = line.Substring(1, line.IndexOf(')') - 1);
                                                        }
                                                        else if (line[0] == '<')
                                                        {
                                                            line = line.Substring(1, line.IndexOf('>') - 1);
                                                        }
                                                        else
                                                        {
                                                            continue;
                                                        }
                                                        line = line.Replace(" ", "");
                                                        line = line.Replace("\"", "");
                                                        index = line.LastIndexOf('.');
                                                        ComponentNames newComp;
                                                        if (index == -1)
                                                        {
                                                            newComp = new ComponentNames(line, "", "");
                                                        }
                                                        else
                                                        {
                                                            newComp = new ComponentNames(line.Substring(index + 1, line.Length - (index + 1)), line.Substring(0, index), "");
                                                        }
                                                        string pName = asset + ", Line " + lineNum;
                                                        newComp.usageSource.Add(pName);
                                                        index = addedComponents.IndexOf(newComp);
                                                        if (index == -1)
                                                        {
                                                            addedComponents.Add(newComp);
                                                        }
                                                        else
                                                        {
                                                            if (!addedComponents[index].usageSource.Contains(pName)) addedComponents[index].usageSource.Add(pName);
                                                        }
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                            }
                                        }
                                        int indexPrefab = asset.IndexOf(".prefab");

                                        if (indexPrefab != -1)
                                        {
                                            string[] single = new string[] { asset };
                                            string[] dependencies = AssetDatabase.GetDependencies(single);
                                            foreach (string dependedAsset in dependencies)
                                            {
                                                if (dependedAsset.IndexOf(".cs") != -1 || dependedAsset.IndexOf(".js") != -1)
                                                {
                                                    ComponentNames newComponent = new ComponentNames(NameFromPath(dependedAsset), GetNamespaceFromPath(dependedAsset), dependedAsset);
                                                    int index = prefabComponents.IndexOf(newComponent);
                                                    if (index == -1)
                                                    {
                                                        newComponent.usageSource.Add(asset);
                                                        prefabComponents.Add(newComponent);
                                                    }
                                                    else
                                                    {
                                                        if (!prefabComponents[index].usageSource.Contains(asset)) prefabComponents[index].usageSource.Add(asset);
                                                    }
                                                }
                                            }
                                        }
                                        int indexUnity = asset.IndexOf(".unity");
                                        if (indexUnity != -1)
                                        {
                                            scenesToLoad.Add(asset);
                                        }
                                    }

                                    for (int i = addedComponents.Count - 1; i > -1; i--)
                                    {
                                        addedComponents[i].assetPath = GetPathFromNames(addedComponents[i].namespaceName, addedComponents[i].componentName);
                                        if (addedComponents[i].assetPath == "") addedComponents.RemoveAt(i);

                                    }

                                    foreach (string scene in scenesToLoad)
                                    {
                                        EditorSceneManager.OpenScene(scene);
                                        GameObject[] sceneGOs = GetAllObjectsInScene();
                                        foreach (GameObject g in sceneGOs)
                                        {
                                            Component[] comps = g.GetComponentsInChildren<Component>(true);
                                            foreach (Component c in comps)
                                            {

                                                if (c != null && c.GetType() != null && c.GetType().BaseType != null && c.GetType().BaseType == typeof(MonoBehaviour))
                                                {
                                                    SerializedObject so = new SerializedObject(c);
                                                    SerializedProperty p = so.FindProperty("m_Script");
                                                    string path = AssetDatabase.GetAssetPath(p.objectReferenceValue);
                                                    ComponentNames newComp = new ComponentNames(NameFromPath(path), GetNamespaceFromPath(path), path);
                                                    newComp.usageSource.Add(scene);
                                                    int index = sceneComponents.IndexOf(newComp);
                                                    if (index == -1)
                                                    {
                                                        sceneComponents.Add(newComp);
                                                    }
                                                    else
                                                    {
                                                        if (!sceneComponents[index].usageSource.Contains(scene)) sceneComponents[index].usageSource.Add(scene);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    foreach (ComponentNames c in existingComponents)
                                    {
                                        if (addedComponents.Contains(c)) continue;
                                        if (prefabComponents.Contains(c)) continue;
                                        if (sceneComponents.Contains(c)) continue;
                                        notUsedComponents.Add(c);
                                    }

                                    addedComponents.Sort(SortAlphabetically);
                                    prefabComponents.Sort(SortAlphabetically);
                                    sceneComponents.Sort(SortAlphabetically);
                                    notUsedComponents.Sort(SortAlphabetically);

                                    EditorUtility.ClearProgressBar();
                                }
                                break;
                        }
                    }
                    break;
                case 1:
                    if (GUILayout.Button("Search!"))
                    {
                        string[] allPrefabs = GetAllPrefabs();
                        string prefab;
                        int numAllPrefabs = allPrefabs == null ? 0 : allPrefabs.Length;
                        List<string> result = new List<string>();
                        for (int i = 0; i < numAllPrefabs; ++i)
                        {
                            prefab = allPrefabs[i];

                            if (EditorUtility.DisplayCancelableProgressBar("Searching..", prefab, i * 1.0f / numAllPrefabs))
                                break;

                            UnityEngine.Object o = AssetDatabase.LoadMainAssetAtPath(prefab);
                            GameObject go;
                            try
                            {
                                go = (GameObject)o;
                                Component[] components = go.GetComponentsInChildren<Component>(true);
                                foreach (Component c in components)
                                {
                                    if (c == null)
                                    {
                                        result.Add(prefab);
                                    }
                                }
                            }
                            catch
                            {
                                Debug.Log("For some reason, prefab " + prefab + " won't cast to GameObject");
                            }
                        }

                        if (listResult != null)
                            listResult.list = result;

                        EditorUtility.ClearProgressBar();
                    }
                    break;
            }
            if (editorMode == 1 || selectedCheckType == 0)
            {
                if (listResult != null && listResult.list != null)
                {
                    if (listResult.count == 0)
                    {
                        GUILayout.Label(editorMode == 0 ? (componentName == "" ? "Choose a component" : "No prefabs use component " + componentName) : ("No prefabs have missing components!\nClick Search to check again"));
                    }
                    else
                    {
                        GUILayout.Label(editorMode == 0 ? ("The following prefabs use component " + componentName + ":") : ("The following prefabs have missing components:"));

                        scroll = GUILayout.BeginScrollView(scroll);
                        listResult.DoLayoutList();
                        GUILayout.EndScrollView();
                        /*scroll = GUILayout.BeginScrollView(scroll);
                        foreach (string s in listResult)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(s, GUILayout.Width(position.width / 2));
                            bool result;
                            Object target;
                            Object[] targets;
                            target = AssetDatabase.LoadMainAssetAtPath(s);
                            result = Selection.Contains(target);
                            if (result != GUILayout.Toggle(result, "Selected", GUILayout.Width(position.width / 2 - 10)))
                            {
                                targets = Selection.objects;

                                if(result)
                                    ArrayUtility.Remove(ref targets, target);
                                else
                                    ArrayUtility.Add(ref targets, target);

                                Selection.objects = targets;
                            }
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndScrollView();*/
                    }
                }
            }
            else
            {
                showPrefabs = GUILayout.Toggle(showPrefabs, "Show prefab components");
                if (showPrefabs)
                {
                    GUILayout.Label("The following components are attatched to prefabs:");
                    DisplayResults(ref scroll1, ref prefabComponents);
                }
                showAdded = GUILayout.Toggle(showAdded, "Show AddComponent arguments");
                if (showAdded)
                {
                    GUILayout.Label("The following components are AddComponent arguments:");
                    DisplayResults(ref scroll2, ref addedComponents);
                }
                showScene = GUILayout.Toggle(showScene, "Show Scene-used components");
                if (showScene)
                {
                    GUILayout.Label("The following components are used by scene objects:");
                    DisplayResults(ref scroll3, ref sceneComponents);
                }
                showUnused = GUILayout.Toggle(showUnused, "Show Unused Components");
                if (showUnused)
                {
                    GUILayout.Label("The following components are not used by prefabs, by AddComponent, OR in any scene:");
                    DisplayResults(ref scroll4, ref notUsedComponents);
                }
            }
        }

        void OnEnable()
        {
            listResult = new ReorderableList(null, typeof(string), true, false, false, true);

            listResult.drawElementCallback = DrawElement;
            listResult.drawElementBackgroundCallback = DrawBackgroud;
            listResult.onRemoveCallback = Remove;
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            List<string> list = listResult == null ? null : listResult.list as List<string>;
            if (list == null)
                return;

            string path = list[index];
            EditorGUI.LabelField(rect, path);

            Event current = Event.current;
            if (current != null && current.type == EventType.MouseUp && rect.Contains(current.mousePosition))
            {
                UnityEngine.Object target = AssetDatabase.LoadMainAssetAtPath(path);

                if (current.control)
                {
                    UnityEngine.Object[] targets = Selection.objects;

                    if (ArrayUtility.Contains(targets, target))
                        ArrayUtility.Remove(ref targets, target);
                    else
                    {
                        ArrayUtility.Add(ref targets, target);

                        Selection.activeObject = target;
                    }

                    Selection.objects = targets;

                    return;
                }

                if (current.shift)
                {
                    UnityEngine.Object[] targets = Selection.objects;
                    if (Selection.Contains(target))
                    {
                        targets = Selection.objects;
                        ArrayUtility.Remove(ref targets, target);

                        Selection.objects = targets;

                        return;
                    }

                    int temp, min = -1;
                    foreach(UnityEngine.Object instance in targets)
                    {
                        temp = list.IndexOf(AssetDatabase.GetAssetPath(instance));
                        if (temp < index && temp > min)
                            min = temp;
                    }

                    if (min == -1)
                        min = index;
                    
                    targets = new UnityEngine.Object[index + 1 - min];
                    for(int i = min; i < index; ++i)
                        targets[i - min] = AssetDatabase.LoadMainAssetAtPath(list[i]);

                    targets[index - min] = target;

                    Selection.objects = targets;

                    return;
                }

                Selection.activeObject = Selection.activeObject == target ? null : target;
            }
                
        }

        void DrawBackgroud(Rect rect, int index, bool isActive, bool isFocused)
        {
            string path = (string)listResult.list[index];

            ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, Selection.Contains(AssetDatabase.LoadMainAssetAtPath(path)), isFocused, true);
        }

        void Remove(ReorderableList list)
        {
            List<string> result = list == null ? null : list.list as List<string>;
            if (result == null)
                return;

            UnityEngine.Object[] targets = Selection.objects;
            if (targets == null)
                return;

            result.RemoveAll(x =>
            {
                foreach(UnityEngine.Object target in targets)
                {
                    if (AssetDatabase.GetAssetPath(target) == x)
                        return true;
                }

                return false;
            });
        }

        int SortAlphabetically(ComponentNames a, ComponentNames b)
        {
            return a.assetPath.CompareTo(b.assetPath);
        }

        GameObject[] GetAllObjectsInScene()
        {
            List<GameObject> objectsInScene = new List<GameObject>();
            GameObject[] allGOs = (GameObject[])Resources.FindObjectsOfTypeAll(typeof(GameObject));
            foreach (GameObject go in allGOs)
            {
                //if ( go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave )
                //    continue;

                string assetPath = AssetDatabase.GetAssetPath(go.transform.root.gameObject);
                if (!string.IsNullOrEmpty(assetPath))
                    continue;

                objectsInScene.Add(go);
            }

            return objectsInScene.ToArray();
        }

        void DisplayResults(ref Vector2 scroller, ref List<ComponentNames> list)
        {
            if (list == null)
                return;
            
            scroller = GUILayout.BeginScrollView(scroller);
            foreach (ComponentNames c in list)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.assetPath, GUILayout.Width(position.width / 5 * 4));
                if (GUILayout.Button("Select", GUILayout.Width(position.width / 5 - 30)))
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(c.assetPath);

                GUILayout.EndHorizontal();
                if (c.usageSource.Count == 1)
                {
                    GUILayout.Label("   In 1 Place: " + c.usageSource[0]);
                }

                if (c.usageSource.Count > 1)
                {
                    GUILayout.Label("   In " + c.usageSource.Count + " Places: " + c.usageSource[0] + ", " + c.usageSource[1] + (c.usageSource.Count > 2 ? ", ..." : ""));
                }
            }
            
            GUILayout.EndScrollView();
        }

        string NameFromPath(string s)
        {
            s = s.Substring(s.LastIndexOf('/') + 1);
            return s.Substring(0, s.Length - 3);
        }

        string GetNamespaceFromPath(string path)
        {
            foreach (ComponentNames c in existingComponents)
            {
                if (c.assetPath == path)
                {
                    return c.namespaceName;
                }
            }
            return "";
        }

        string GetPathFromNames(string space, string name)
        {
            ComponentNames test = new ComponentNames(name, space, "");
            int index = existingComponents.IndexOf(test);
            if (index != -1)
            {
                return existingComponents[index].assetPath;
            }
            return "";
        }

        public static string[] GetAllPrefabs()
        {
            string[] temp = AssetDatabase.GetAllAssetPaths();
            List<string> result = new List<string>();
            foreach (string s in temp)
            {
                if (s.Contains(".prefab")) result.Add(s);
            }
            return result.ToArray();
        }
    }
}