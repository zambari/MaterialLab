namespace MaterialLab.Editor
{
	using System;

	using MaterialLab.Tabs;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class MaterialLabWindow : MaterialLabBaseWindow
	{
		[MenuItem(MenuPathBase + "Material Lab", false)]
		private static void OpenWindow()
		{
			var inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.dll");
			var window = EditorWindow.GetWindow<MaterialLabWindow>(new Type[] { inspectorType });
			window.titleContent = new GUIContent("Material Lab");
		}

		public void CreateGUI()
		{
			var textureTab = new TextureEditTab();
			var materialTab = new MaterialTab();
			var textureCombinerTab = new TextureCombinerTab();
			var content = new VisualElement();
			content.SetPadding(5);
			content.style.height = Length.Percent(100);
			var tabs = new TabSelector(content, nameof(MaterialLabWindow), textureTab, textureCombinerTab, materialTab);
			rootVisualElement.Add(tabs);
			rootVisualElement.Add(content);
		}
	}
}
