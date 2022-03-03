using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FedoraDev.PointOfInterest.Editor
{
	public class PointOfInterestNodeEditor : EditorWindow
	{
		#region Properties
		const string EDITOR_PREF_LAST_OPENED_KEY = "Last Node Web";
		const string FACTORY_PATH = "Assets/Fedora Dev/";
		const string FACTORY_NAME = "Point Of Interest Factory.asset";
		static string FactoryAsset => $"{FACTORY_PATH}{FACTORY_NAME}";

		public INodeWeb NodeWeb { get => _nodeWeb; set => _nodeWeb = value; }

		IFactory FactoryInstance
		{
			get
			{
				if (_factoryInstance == null)
					_factoryInstance = (IFactory)AssetDatabase.LoadAssetAtPath(FactoryAsset, typeof(IFactory));

				if (_factoryInstance == null)
					throw new NullReferenceException($"No factory reference found at {FactoryAsset}. Please create one!");

				return _factoryInstance;
			}
		}

		[OdinSerialize] INodeWeb _nodeWeb;

		IFactory _factoryInstance;
		string _targetLocation = "";
		GUIStyle _nodeStyle;
		GUIStyle _bridgeStyle;
		GUIStyle _textStyle;
		GUIStyle _connectionStyle;
		GUIStyle _uiPositionStyle;
		Type[] _nodeWebClasses;
		INode _connectingFromNode;
		bool _connectingNodes;
		#endregion

		#region Initialization
		[MenuItem("Tools/Point of Interest Editor")]
		public static void OpenWindow()
		{
			PointOfInterestNodeEditor window = GetWindow<PointOfInterestNodeEditor>();
			if (window.NodeWeb == null)
			{
				string assetPath = EditorPrefs.GetString(EDITOR_PREF_LAST_OPENED_KEY);
				if (assetPath == string.Empty)
				{
					window.titleContent = new GUIContent("POI Web - New");
					return;
				}

				INodeWeb asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(ScriptableObject)) as INodeWeb;
				if (asset == null)
				{
					window.titleContent = new GUIContent("POI Web - New");
					EditorPrefs.SetString(EDITOR_PREF_LAST_OPENED_KEY, string.Empty);
					return;
				}

				window.NodeWeb = asset;
				window.titleContent = new GUIContent($"POI Web - {window.NodeWeb.Name}");
			}
			else
				window.titleContent = new GUIContent($"POI Web - {window.NodeWeb.Name}");
		}

		public static void OpenWindow(INodeWeb nodeWeb)
		{
			PointOfInterestNodeEditor window = GetWindow<PointOfInterestNodeEditor>();
			window.NodeWeb = nodeWeb;
			window.titleContent = new GUIContent($"POI Web - {window.NodeWeb.Name}");
			EditorPrefs.SetString(EDITOR_PREF_LAST_OPENED_KEY, AssetDatabase.GetAssetPath(nodeWeb as ScriptableObject));
		}

		void OnEnable()
		{
			_nodeStyle = new GUIStyle();
			//_nodeStyle.normal.background = new Texture2D(1, 1);
			_nodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1.png") as Texture2D;
			_nodeStyle.border = new RectOffset(12, 12, 12, 12);

			_bridgeStyle = new GUIStyle(_nodeStyle);
			_bridgeStyle.alignment = TextAnchor.MiddleCenter;

			_textStyle = new GUIStyle();
			_textStyle.alignment = TextAnchor.MiddleCenter;

			_connectionStyle = new GUIStyle(_textStyle);
			_connectionStyle.normal.background = new Texture2D(1, 1);

			_uiPositionStyle = new GUIStyle();
			_uiPositionStyle.normal.background = new Texture2D(1, 1);
			_uiPositionStyle.alignment = TextAnchor.MiddleCenter;

			_nodeWebClasses = GetAllThatImplement<INodeWeb>();

			//if (NodeWeb != null)
			//{
			//	for (int i = 0; i < NodeWeb.Nodes.Length; i++)
			//		NodeWeb.Nodes[i].Position = new Rect(NodeWeb.Nodes[i].Position.x, NodeWeb.Nodes[i].Position.y, NodeWeb.Nodes[i].Size.x, NodeWeb.Nodes[i].Size.y);
			//	for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//		NodeWeb.Bridges[i].Position = new Rect(NodeWeb.Bridges[i].Position.x, NodeWeb.Bridges[i].Position.y, NodeWeb.Bridges[i].Size.x, NodeWeb.Bridges[i].Size.y);
			//}
		}

		private void OnDisable()
		{
			NodeWeb = null;
		}

		public static Type[] GetAllThatImplement<T>()
		{
			Type interfaceType = typeof(T);
			Type[] classes = AppDomain.CurrentDomain.GetAssemblies()
						  .SelectMany(assembly => assembly.GetTypes())
						  .Where(cls => interfaceType.IsAssignableFrom(cls) && cls.IsClass)
						  .ToArray();

			return classes;
		}
		#endregion

		#region OnGUI
		private void OnGUI()
		{
			if (NodeWeb == null)
				DrawEmptyEditor();
			else
				DrawNodeEditor();

			if (GUI.changed)
				Repaint();
		}

		void DrawEmptyEditor()
		{
			float width = position.width < 500 ? position.width : 500;

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical(GUILayout.Width(width));
			GUILayout.FlexibleSpace();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label("Create a new Node Web:");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label("Assets/");
			_targetLocation = GUILayout.TextField(_targetLocation, GUILayout.MinWidth(100f));
			GUILayout.Label("/Node Web.asset");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			for (int i = 0; i < _nodeWebClasses.Length; i++)
			{
				int index = i;
				string className = ObjectNames.NicifyVariableName(_nodeWebClasses[index].Name);
				if (GUILayout.Button($"{className}"))
				{
					NodeWeb = _nodeWebClasses[index].GetMethod("ProduceInEditor").Invoke(null, new object[] { $"Assets/{_targetLocation}/Node Web.asset" }) as INodeWeb;
					NodeWeb.Offset = new Vector2(position.width / 2, position.height / 2);
					AssetDatabase.SaveAssets();
				}
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

		void DrawNodeEditor()
		{
			DrawGrid(20, 0.2f, Color.gray);
			DrawGrid(100, 0.4f, Color.gray);
			DrawZero();
			DrawMouseConnectionLine(Event.current);
			DrawBridges();
			DrawNodes();
			DrawUI();
			ProcessConnectionEvents(Event.current);
			ProcessNodeEvents(Event.current);
			ProcessBridgeEvents(Event.current);
			ProcessEvents(Event.current);
		}
		#endregion

		#region Drawing
		void DrawGrid(float spacing, float opacity, Color color)
		{
			int widthDivs = Mathf.CeilToInt(position.width / spacing);
			int heightDivs = Mathf.CeilToInt(position.height / spacing);

			Handles.BeginGUI();
			Handles.color = new Color(color.r, color.g, color.b, opacity);

			float offsetX = Mathf.Abs(NodeWeb.Offset.x) % spacing;
			float offsetY = Mathf.Abs(NodeWeb.Offset.y) % spacing;

			if (NodeWeb.Offset.x < 0)
				offsetX = spacing - offsetX;

			if (NodeWeb.Offset.y < 0)
				offsetY = spacing - offsetY;

			Vector3 offset = new Vector3(Mathf.Round(offsetX / 10) * 10, Mathf.Round(offsetY / 10) * 10, 0f);

			for (int i = 0; i < widthDivs; i++)
				Handles.DrawLine(new Vector3(spacing * i, -spacing, 0f) + offset, new Vector3(spacing * i, position.height, 0f) + offset);

			for (int i = 0; i < heightDivs; i++)
				Handles.DrawLine(new Vector3(-spacing, spacing * i, 0f) + offset, new Vector3(position.width, spacing * i, 0f) + offset);

			Handles.EndGUI();
		}

		void DrawZero()
		{
			Handles.BeginGUI();
			Handles.color = Color.blue;
			float offsetX = Mathf.Round(NodeWeb.Offset.x / 10) * 10;
			float offsetY = Mathf.Round(NodeWeb.Offset.y / 10) * 10;

			if (NodeWeb.Offset.x > -5f && NodeWeb.Offset.x < position.width + 5f)
				Handles.DrawLine(new Vector3(offsetX, -5, 0), new Vector3(offsetX, position.height + 5, 0), 2f); //Vertical Line

			if (NodeWeb.Offset.y > -5f && NodeWeb.Offset.y < position.height + 5f)
				Handles.DrawLine(new Vector3(-5, offsetY, 0), new Vector3(position.width + 5, offsetY, 0), 2f); //Horizontal Line
			Handles.EndGUI();
		}

		void DrawMouseConnectionLine(Event currentEvent)
		{
			if (_connectingNodes)
			{
				Handles.color = Color.blue;
				Handles.DrawLine(new Vector3(_connectingFromNode.Position.x + (_connectingFromNode.Size.x / 2), _connectingFromNode.Position.y + (_connectingFromNode.Size.y / 2), 0),
								 new Vector3(currentEvent.mousePosition.x, currentEvent.mousePosition.y,
							 	 0), 4f);
				GUI.changed = true;
			}
		}

		void DrawNodes()
		{
			for (int i = 0; i < NodeWeb.Nodes.Length; i++)
			{
				INode node = NodeWeb.Nodes[i];
				//Rect textBox = node.Position;
				//textBox.height -= 10;
				//textBox.y += 5;
				//Rect objectBox = textBox;
				//Rect connectionBox = textBox;
				//float boxHeight = textBox.height / 2f;

				//objectBox.height = boxHeight;
				//connectionBox.height = boxHeight - 12;
				//connectionBox.width = textBox.width - 24;

				//objectBox.position = new Vector2(objectBox.position.x, objectBox.position.y);
				//connectionBox.position = new Vector2(connectionBox.position.x + 12, connectionBox.position.y + boxHeight);

				//GUI.Box(node.Position, string.Empty, _nodeStyle);
				//GUI.Label(objectBox, node.PointOfInterest == null ? "No PoI" : node.PointOfInterest.Name, _textStyle);
				//GUI.Label(connectionBox, "->", _connectionStyle);
			}
		}

		void DrawBridges()
		{
			//for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//{
			//	Handles.BeginGUI();
			//	Handles.color = Color.white;
			//	INodeBridge bridge = NodeWeb.Bridges[i];

			//	Vector3 pos = new Vector3(bridge.Position.x + (bridge.Size.x / 2), bridge.Position.y + (bridge.Size.y / 2), 0);
			//	for (int j = 0; j < bridge.Connections.Length; j++)
			//	{
			//		//INode node = bridge.Connections[j].Node;
			//		//Rect nodePos = node.Position;
			//		//Vector2 deletePosition = (new Vector2(node.Position.x + (node.Size.x / 2), node.Position.y + (node.Size.y / 2)) + new Vector2(bridge.Position.x + (bridge.Size.x / 2), bridge.Position.y + (bridge.Size.y / 2))) / 2;
			//		//Vector2 floatPosition = deletePosition + new Vector2(-25, 20);

			//		//Handles.DrawLine(new Vector3(nodePos.x + (node.Size.x / 2), nodePos.y + (node.Size.y / 2), 0), pos, 3f);
			//		//if (GUI.Button(new Rect(deletePosition.x - 10, deletePosition.y - 10, 20, 20), "x"))
			//		//{
			//		//	node.RemoveConnection(bridge);
			//		//	bridge.RemoveConnection(node);
			//		//	GUI.changed = true;
			//		//	return;
			//		//}

			//		//float oldValue = bridge.Connections[j].Distance;
			//		//bridge.Connections[j].Distance = EditorGUI.FloatField(new Rect(floatPosition.x, floatPosition.y, 50, 20), bridge.Connections[j].Distance);
			//		//if (bridge.Connections[j].Distance != oldValue)
			//		//{
			//		//	AssetDatabase.SaveAssets();
			//		//	GUI.changed = true;
			//		//	return;
			//		//}
			//	}
			//	Handles.EndGUI();

			//	GUI.Box(bridge.Position, bridge.Name, _bridgeStyle);
			//}
		}

		void DrawUI()
		{
			//float width = 100f;
			//float btnWidth = 100;
			//GUI.Box(new Rect(position.width - width, 0, width, 25), $"({NodeWeb.Offset.x}, {NodeWeb.Offset.y})", _uiPositionStyle);
			//if (GUI.Button(new Rect(position.width - width - btnWidth, 0, btnWidth, 25), "Recenter"))
			//{
			//	Vector2 objectCenter = Vector2.zero;

			//	for (int i = 0; i < NodeWeb.Nodes.Length; i++)
			//		objectCenter += NodeWeb.Nodes[i].Position.position + (NodeWeb.Nodes[i].Position.size / 2);

			//	for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//		objectCenter += NodeWeb.Bridges[i].Position.position + (NodeWeb.Bridges[i].Position.size / 2);

			//	objectCenter /= (NodeWeb.Nodes.Length + NodeWeb.Bridges.Length);
			//	Vector2 offset = -objectCenter;
			//	offset = new Vector2(Mathf.Round(offset.x / 100) * 100, Mathf.Round(offset.y / 100) * 100);

			//	for (int i = 0; i < NodeWeb.Nodes.Length; i++)
			//	{
			//		NodeWeb.Nodes[i].Move(offset + (position.size / 2));
			//		//NodeWeb.Nodes[i].Place();
			//	}

			//	for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//	{
			//		NodeWeb.Bridges[i].Move(offset + (position.size / 2));
			//		NodeWeb.Bridges[i].Place();
			//	}

			//	NodeWeb.Offset = new Vector2(position.width / 2, position.height / 2);

			//	AssetDatabase.SaveAssets();
			//}
		}
		#endregion

		#region Processing
		void ProcessConnectionEvents(Event currentEvent)
		{
			switch (currentEvent.type)
			{
				case EventType.MouseDown:
					if (currentEvent.button == 0)
					{
						//for (int i = 0; i < NodeWeb.Nodes.Length; i++)
						//{
						//	INode node = NodeWeb.Nodes[i];
						//	float thirdHeight = node.Position.height / 2f;
						//	Rect connectionRect = new Rect(node.Position.x, thirdHeight + node.Position.y, node.Position.width, thirdHeight);
						//	if (connectionRect.Contains(currentEvent.mousePosition))
						//	{
						//		_connectingNodes = true;
						//		_connectingFromNode = node;
						//		currentEvent.Use();
						//		GUI.changed = true;
						//		return;
						//	}
						//}
					}
					break;

				case EventType.MouseUp:
					if (_connectingNodes && currentEvent.button == 0)
					{
						//for (int i = 0; i < NodeWeb.Nodes.Length; i++)
						//{
						//	INode node = NodeWeb.Nodes[i];
						//	if (node.Position.Contains(currentEvent.mousePosition))
						//	{
						//		if (_connectingFromNode == node)
						//			continue;

						//		IConnection nodeAConnection = FactoryInstance.ProduceNodeBridgeConnection();
						//		IConnection nodeBConnection = FactoryInstance.ProduceNodeBridgeConnection();
						//		INodeBridge bridge = Activator.CreateInstance(GetAllThatImplement<INodeBridge>()[0]) as INodeBridge;

						//		nodeAConnection.Distance = 0;
						//		//nodeAConnection.Node = node;
						//		//nodeAConnection.Bridge = bridge;

						//		nodeBConnection.Distance = 0;
						//		//nodeBConnection.Node = _connectingFromNode;
						//		//nodeBConnection.Bridge = bridge;

						//		Vector2 bridgePosition = (_connectingFromNode.Position.position + node.Position.position) / 2;

						//		bridge.Position = new Rect(Vector2.zero, bridge.Size);
						//		bridge.Move(bridgePosition);
						//		bridge.AddConnection(nodeAConnection);
						//		bridge.AddConnection(nodeBConnection);
						//		NodeWeb.Bridges = NodeWeb.Bridges.Append(bridge).ToArray();
						//		node.AddConnection(nodeBConnection);
						//		_connectingFromNode.AddConnection(nodeAConnection);

						//		AssetDatabase.SaveAssets();
						//	}
						//}

						//for (int i = 0; i < NodeWeb.Bridges.Length; i++)
						//{
						//	INodeBridge bridge = NodeWeb.Bridges[i];
						//	if (bridge.Position.Contains(currentEvent.mousePosition))
						//	{
						//		//bool cont = false;
						//		//for (int j = 0; j < bridge.Connections.Length; j++)
						//		//	if (bridge.Connections[j].Node == _connectingFromNode)
						//		//		cont = true;
						//		//if (cont) continue;

						//		IConnection nodeConnection = FactoryInstance.ProduceNodeBridgeConnection();

						//		nodeConnection.Distance = 0;
						//		//nodeConnection.Node = _connectingFromNode;
						//		//nodeConnection.Bridge = bridge;

						//		bridge.AddConnection(nodeConnection);
						//		_connectingFromNode.AddConnection(nodeConnection);
						//		AssetDatabase.SaveAssets();
						//	}
						//}

						_connectingNodes = false;
						_connectingFromNode = null;
					}
					break;
			}
		}

		void ProcessNodeEvents(Event currentEvent)
		{
			for (int i = 0; i < NodeWeb.Nodes.Length; i++)
			{
				//if (NodeWeb.Nodes[i].ProcessEvents(currentEvent))
				//	GUI.changed = true;
			}
		}

		void ProcessBridgeEvents(Event currentEvent)
		{
			//for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//{
			//	if (NodeWeb.Bridges[i].ProcessEvents(currentEvent))
			//		GUI.changed = true;
			//}
		}

		void ProcessEvents(Event currentEvent)
		{
			switch (currentEvent.type)
			{
				case EventType.MouseDown:
					if (currentEvent.button == 1)
						ProcessContextMenu(currentEvent.mousePosition);
					break;

				case EventType.MouseDrag:
					if (currentEvent.button == 2)
						OnDrag(currentEvent.delta);
					break;

				case EventType.MouseUp:
					if (currentEvent.button == 2)
					{
						//for (int i = 0; i < NodeWeb.Nodes.Length; i++)
						//	NodeWeb.Nodes[i].Place();
						AssetDatabase.SaveAssets();
					}
					break;

				case EventType.DragUpdated:
					if ((DragAndDrop.objectReferences[0] as IPointOfInterest) != null)
					{
						DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
						DragAndDrop.AcceptDrag();
					}
					break;

				case EventType.DragPerform:
					for (int i = 0; i < NodeWeb.Nodes.Length; i++)
					{
						//if (NodeWeb.Nodes[i].Position.Contains(currentEvent.mousePosition))
						//	NodeWeb.Nodes[i].PointOfInterest = DragAndDrop.objectReferences[0] as IPointOfInterest;
						currentEvent.Use();
						GUI.changed = true;
					}

					for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
					{
						IPointOfInterest poi = DragAndDrop.objectReferences[i] as IPointOfInterest;
						if (poi == null)
							continue;

						INode node;

						//if (NodeWeb.Nodes.Length == 0)
						//	node = Activator.CreateInstance(GetAllThatImplement<INode>()[0]) as INode;
						//else
						//	node = NodeWeb.Nodes[NodeWeb.Nodes.Length - 1].CreateCopy();
						//node.Position = new Rect(currentEvent.mousePosition - node.Size, node.Size);
						//node.PointOfInterest = poi;
						//NodeWeb.Nodes = NodeWeb.Nodes.Append(node).ToArray();
						AssetDatabase.SaveAssets();
					}
					break;
			}
		}

		void ProcessContextMenu(Vector2 mousePosition)
		{
			GenericMenu genericMenu = new GenericMenu();

			bool added1 = false;
			for (int i = 0; i < NodeWeb.Nodes.Length; i++)
			{
				INode node = NodeWeb.Nodes[i];
				//if (node.Position.Contains(mousePosition))
				//{
				//	genericMenu.AddItem(new GUIContent($"Remove {node.Name}"), false, () => OnClickRemoveNode(node));
				//	added1 = true;
				//}
			}

			if (added1)
				genericMenu.AddSeparator(string.Empty);

			added1 = false;
			//for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//{
			//	INodeBridge bridge = NodeWeb.Bridges[i];
			//	if (bridge.Position.Contains(mousePosition))
			//	{
			//		genericMenu.AddItem(new GUIContent($"Remove {bridge.Name}"), false, () => OnClickRemoveBridge(bridge));
			//		added1 = true;
			//	}
			//}

			if (added1)
				genericMenu.AddSeparator(string.Empty);

			Type[] iNodes = GetAllThatImplement<INode>();
			for (int i = 0; i < iNodes.Length; i++)
			{
				int index = i;
				genericMenu.AddItem(new GUIContent($"Add {iNodes[index].Name}"), false, () => OnClickAddNode(iNodes[index], mousePosition));
			}

			genericMenu.AddSeparator(string.Empty);

			Type[] iBridges = GetAllThatImplement<INodeBridge>();
			for (int i = 0; i < iBridges.Length; i++)
			{
				int index = i;
				genericMenu.AddItem(new GUIContent($"Add {iBridges[index].Name}"), false, () => OnClickAddBridge(iBridges[index], mousePosition));
			}

			genericMenu.ShowAsContext();
		}
		#endregion

		#region OnEvents
		void OnDrag(Vector2 delta)
		{
			NodeWeb.Offset += delta;

			for (int i = 0; i < NodeWeb.Nodes.Length; i++)
				NodeWeb.Nodes[i].Move(delta);

			//for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//	NodeWeb.Bridges[i].Move(delta);

			GUI.changed = true;
		}

		void OnClickAddNode(Type nodeType, Vector2 mousePosition)
		{
			INode node = Activator.CreateInstance(nodeType) as INode;
			//node.Position = new Rect(mousePosition, node.Size);

			//NodeWeb.Nodes = NodeWeb.Nodes.Append(node).ToArray();
			AssetDatabase.SaveAssets();
		}

		void OnClickAddBridge(Type bridgeType, Vector2 mousePosition)
		{
			INodeBridge bridge = Activator.CreateInstance(bridgeType) as INodeBridge;
			bridge.Position = new Rect(mousePosition, bridge.Size);

			//NodeWeb.Bridges = NodeWeb.Bridges.Append(bridge).ToArray();
			AssetDatabase.SaveAssets();
		}

		void OnClickRemoveNode(INode node)
		{
			for (int i = 0; i < node.Connections.Length; i++)
			{
				//INodeBridge bridge = node.Connections[i].Bridge;
				//bridge.RemoveConnection(node);
			}

			List<INode> nodes = new List<INode>();
			for (int i = 0; i < NodeWeb.Nodes.Length; i++)
				if (NodeWeb.Nodes[i] != node)
					nodes.Add(NodeWeb.Nodes[i]);
			NodeWeb.Nodes = nodes.ToArray();
			AssetDatabase.SaveAssets();
		}

		void OnClickRemoveBridge(INodeBridge bridge)
		{
			for (int i = 0; i < bridge.Connections.Length; i++)
			{
				IConnection connection = bridge.Connections[i];
				//connection.Node.RemoveConnection(bridge);
			}

			List<INodeBridge> bridges = new List<INodeBridge>();
			//for (int i = 0; i < NodeWeb.Bridges.Length; i++)
			//	if (NodeWeb.Bridges[i] != bridge)
			//		bridges.Add(NodeWeb.Bridges[i]);
			//NodeWeb.Bridges = bridges.ToArray();
			AssetDatabase.SaveAssets();
		}
		#endregion
	}
}