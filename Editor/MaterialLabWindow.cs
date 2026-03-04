namespace MaterialLab.Editor
{
	using System;

	using MaterialLab.Tabs;

	using UnityEditor;

	using UnityEngine;
	using UnityEngine.UIElements;

	public class MaterialLabWindow : MaterialLabBaseWindow
	{
		[MenuItem(MenuPathBase + "Material Lab1", false)]
		private static void OpenWindow()
		{
			var inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.dll");
			var window = EditorWindow.GetWindow<MaterialLabWindow>(new Type[] { inspectorType });
			window.titleContent = new GUIContent("Material Lab");
		}

		private VisualElement contextElement;

		public void CreateGUI()
		{
			var textureTab = new TextureEditTab();
			var materialTab = new MaterialTab();
			var content = new VisualElement();
			content.style.height = Length.Percent(100);
			content.style.backgroundColor = Color.red;
			var tabs = new TabSelector(content, nameof(MaterialLabWindow), textureTab, materialTab);

			rootVisualElement.Add(tabs);
			// rootVisualElement.Add(content);
		}
	}
}
