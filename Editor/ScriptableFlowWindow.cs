using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ScriptableFlow.Editor.Helpers;
using ScriptableFlow.Runtime.Attributes;
using UnityEditor;
using UnityEngine;

namespace ScriptableFlow.Editor
{
    public class ScriptableFlowWindow : EditorWindow
    {
        public static readonly Regex s_fileNameRegex = new(@"[^A-Za-z0-9._\-\(\)]", RegexOptions.Compiled);
        
        private struct Entry
        {
            public Type type;
            public string name;
            public string fullName;
            public string category;
        }

        private Entry[] _entries;
        private string[] _categories;
                            
        private string      _filter;
        private bool        _ignoreCase;
        private Entry[]     _filteredEntries;
        private string[]    _filteredEntryNames;
        private int         _selectedIndex;
        private int         _selectedCategory;
        private string      _fileName;
        private string      _newAssetPath;
                                    
        private ScriptableObject    _newInstance;
        private UnityEditor.Editor  _newInstanceEditor;
        private ScriptableObject    _template;
        private UnityEditor.Editor  _templateEditor;
        private Exception           _inlineInstanceException;
        private Vector2             _scrollPosition;

        [MenuItem("Assets/Create/Scriptable Object", priority = -1)]
        public static void OpenWindow()
        {
            ScriptableFlowWindow window = GetWindow<ScriptableFlowWindow>();
            window.titleContent = new GUIContent("Scriptable Flow");
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
            var categoriesSet = new HashSet<string>();
            categoriesSet.Add("All");
            List<Entry> validEntries = new();
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly
                    .GetTypes()
                    .Where(IsValidScriptableObject);
                
                var newEntries = types.Select(validType =>
                {
                    var nearestType = ReflectionStatics.GetNearestTypeOrInterfaceWithAttribute<CreateAssetAttribute>(validType);
                    var createAttribute = nearestType?.GetCustomAttribute<CreateAssetAttribute>();
                    var category = createAttribute != null ? createAttribute.category : "None";
                    categoriesSet.Add(category);
                    return new Entry
                    {
                        type = validType,
                        name = validType.Name,
                        fullName = validType.FullName,
                        category = category,
                    };
                });
                validEntries.AddRange(newEntries);
            }
            
            _categories = categoriesSet.ToArray();
            Array.Sort(_categories, (a, b) =>
            {
                if (a == "All")
                    return -1;
                if (b == "All")
                    return 1;
                if (a == "None")
                    return -1;
                if (b == "None")
                    return 1;
                return String.CompareOrdinal(a, b);
            });
            _entries = validEntries.ToArray();
            _filteredEntries = validEntries.ToArray();
            _filteredEntryNames = _filteredEntries.Select(filteredEntry => filteredEntry.fullName).ToArray();
            _fileName = _filteredEntries.Length > 0 ? _filteredEntries[_selectedIndex].name : string.Empty;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            EditorGUIFunctions.DrawHeaderText("SETTINGS");
            
            bool hasEntries = _filteredEntries.Length > 0;
            var selectedIndex = EditorGUILayout.Popup(_selectedIndex, _filteredEntryNames);
            bool indexChanged = selectedIndex != _selectedIndex;
            _selectedIndex = selectedIndex;

            var filter = EditorGUILayout.TextField("Type Filter", _filter);
            var ignoreCase = EditorGUILayout.Toggle("Ignore Case", _ignoreCase);
            var selectedCategory = EditorGUILayout.Popup("Category", _selectedCategory, _categories);

            bool ignoreCaseUpdated = _ignoreCase != ignoreCase;
            bool categoryUpdated = _selectedCategory != selectedCategory;
            bool filterUpdated = filter != _filter || _filteredEntries == null || ignoreCaseUpdated || categoryUpdated;

            _ignoreCase = ignoreCase;
            _selectedCategory = selectedCategory;

            if (filterUpdated)
            {
                var stringComparison = _ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                _filter = filter;
                _filteredEntries = _entries.Where(entry =>
                {
                    if (_selectedCategory == 0) // All
                        return string.IsNullOrEmpty(filter) || entry.fullName.Contains(_filter, stringComparison);
                    
                    return (string.IsNullOrEmpty(filter) || entry.fullName.Contains(_filter, stringComparison)) && entry.category == _categories[_selectedCategory];
                }).ToArray();
                hasEntries = _filteredEntries.Length > 0;
                _filteredEntryNames = _filteredEntries.Select(filteredEntry => filteredEntry.fullName).ToArray();
                _selectedIndex = 0;
                indexChanged = true;
                _template = null;
                _templateEditor = null;
                _newInstance = null;
                _newInstanceEditor = null;
            }
            
            if (indexChanged || !_newInstance)
            {
                _newInstance = _filteredEntries.Length > 0 ? CreateInstance(_filteredEntries[_selectedIndex].type) : null;
                _newInstanceEditor = _newInstance ? UnityEditor.Editor.CreateEditor(_newInstance) : null;
                _inlineInstanceException = null;
            }
            
            using (new EditorGUI.DisabledScope(!hasEntries))
            {
                EditorGUILayout.LabelField("Type", hasEntries ? _filteredEntries[_selectedIndex].name : "");

                if (filterUpdated || indexChanged)
                    _fileName = hasEntries ? _filteredEntries[_selectedIndex].name : string.Empty;

                _fileName = EditorGUILayout.TextField("File Name", _fileName);
                _fileName = s_fileNameRegex.Replace(_fileName, string.Empty);
                
                bool assetPathValid = TryGetActiveNewAssetPath(_fileName, out _newAssetPath) && 
                                      !File.Exists(_newAssetPath) && 
                                      !string.IsNullOrWhiteSpace(_fileName);
                if (!assetPathValid && hasEntries)
                    EditorGUILayout.HelpBox($"The selected asset path is invalid. Path: {_newAssetPath}", MessageType.Error);

                var template = (ScriptableObject)EditorGUILayout.ObjectField("Template", _template,
                    hasEntries ? _filteredEntries[_selectedIndex].type : typeof(ScriptableObject), false);

                bool templateUpdated = _template != template;
                if (templateUpdated)
                {
                    _template = template;
                    _templateEditor = _template ? UnityEditor.Editor.CreateEditor(_template) : null;
                }
                if (_template != null)
                {
                    _newInstance = null;
                    _newInstanceEditor = null;
                }

                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUI.BeginDisabledGroup(!assetPathValid);
                if (GUILayout.Button("Create"))
                {
                    OnCreateButtonPressed();
                }
                EditorGUI.EndDisabledGroup();
                
                GUILayout.Space(10);
                
                if (_templateEditor)
                {
                    EditorGUIFunctions.DrawHeaderText("TEMPLATE VIEWER");
                    if (GUILayout.Button("Edit"))
                    {
                        _newInstance = Instantiate(_template);
                        _newInstanceEditor = UnityEditor.Editor.CreateEditor(_newInstance);
                        _template = null;
                        _templateEditor = null;
                    }
                    else
                    {
                        using var templateDisabledScope = new EditorGUI.DisabledScope(true);
                        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                        _templateEditor.OnInspectorGUI();
                        EditorGUILayout.EndScrollView();
                    }
                }
                else if (_newInstanceEditor)
                {
                    EditorGUIFunctions.DrawHeaderText("INSTANCE EDITOR");
                    if (GUILayout.Button("Reset"))
                    {
                        _newInstance = null;
                        _newInstanceEditor = null;
                    }
                    else
                    {
                        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                        DrawEditorSafely(_newInstanceEditor, ref _inlineInstanceException);
                        if (_inlineInstanceException != null)
                        {
                            EditorGUILayout.HelpBox(_inlineInstanceException.ToString(), MessageType.Error);
                        }
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
        }

        private void OnCreateButtonPressed()
        {
            ScriptableObject instance;
            if (_template == null)
            {
                instance = _newInstance;
                AssetDatabase.CreateAsset(instance, _newAssetPath);
                _newInstance = null;
                _newInstanceEditor = null;
            }
            else
            {
                var templatePath = AssetDatabase.GetAssetPath(_template);
                AssetDatabase.CopyAsset(templatePath, _newAssetPath);
                instance = AssetDatabase.LoadAssetAtPath<ScriptableObject>(_newAssetPath);
            }
            Selection.activeObject = instance;
        }

        private static void DrawEditorSafely(UnityEditor.Editor editor, ref Exception exception)
        {
            try
            {
                editor.OnInspectorGUI();
            }
#pragma warning disable CS0168
            catch (ExitGUIException _)
#pragma warning restore CS0168
            {
                // Swallowed intentionally.
                // Certain built-in types (Color, AnimationCurve ...) can trigger early exit.
            }
            catch (Exception e)
            {
                exception = e;
            }
        }

        private static bool IsValidScriptableObject(Type type)
        {
            if (!type.IsSubclassOf(typeof(ScriptableObject)))
                return false;

            if (!type.HasTypeOrInterfaceWithAttribute<CreateAssetAttribute>() &&
                !Attribute.IsDefined(type, typeof(CreateAssetMenuAttribute), inherit: false))
                return false;

            var nearestType = type.GetNearestTypeOrInterfaceWithAttribute<CreateAssetAttribute>();
            if (nearestType != null && nearestType != type && !type.GetCustomAttribute<CreateAssetAttribute>().inherit)
                return false;
            
            if (type.IsAbstract || type.IsGenericType || type.IsNotPublic)
                return false;

            if (string.IsNullOrEmpty(type.FullName))
                return false;

            return true;
        }

        private static bool TryGetActiveNewAssetPath(string fileName, out string assetPath)
        {
            assetPath = null;
            if (!TryGetActiveFolderPath(out string folderPath))
                return false;

            assetPath = Path.Combine(folderPath, $"{fileName}.asset");
            return true;
        }
        
        private static bool TryGetActiveFolderPath(out string path)
        {
            path = string.Empty;
            var methodInfo = typeof(ProjectWindowUtil).GetMethod("TryGetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (methodInfo == null)
                return false;

            object[] args = { null };
            bool found = (bool)methodInfo!.Invoke(null, args);
            path = (string)args[0];

            return found;
        }
    }
}