﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Drawing;

namespace ArtemisMissionEditor
{
	public sealed class Mission
	{
		private static Mission _current;
		public static Mission Current { get { return _current; } set { _current = value; } }

        private bool ___STATIC_E_statementTV_SuppressSelectionEvents;//flag to supress selection events while pasting
        private bool ___STATIC_E_nodeTV_SupressSelectionEvents;//flag to supress selection events while pasting
		private bool ___STATIC_E_nodeTV_SupressExpandCollapseEvents;//flag to supress afterexpand and aftercollapse events while mass-operating
		private int ___STATIC_Update_Counter;//Semaphore for begin/end update

		#region Name lists

        //The list of names of objects present in such statements as creating set_variable create set_timer
		private List<string> _variables, _timers;
        private List<string> _variableHeaders, _timerHeaders;
        private Dictionary<string,List<string>> _nameds;
        public KeyValuePair<List<string>,List<string>> VariableNamesList { get { return new KeyValuePair<List<string>,List<string>>(_variables,_variableHeaders); } }
        public KeyValuePair<List<string>, List<string>> TimerNamesList { get { return new KeyValuePair<List<string>,List<string>>(_timers,_timerHeaders); } }
		public KeyValuePair<List<string>, List<string>> StationNamesList { get { return new KeyValuePair<List<string>, List<string>>(_nameds["station"], new List<string>()); } }
		public Dictionary<string, List<string>> AllNamesLists { get { return _nameds; } }

		#endregion

		#region Mission inner parameters

        private string _filePath;
		public string FilePath { get { return _filePath; } set { _filePath = value; _form.UpdateFormText(); } }

		private TreeNode _startNode;
		public TreeNode StartNode { get { return _startNode; } }
		/// <summary> Node whose create statements are used as background </summary>
		private TreeNode _bgNode;
		/// <summary> Node whose create statements are used as background </summary>
		public TreeNode BgNode { get { return _bgNode; } set { _bgNode = value; } }
		public string _commentsBefore;
		public string _commentsAfter;

		private int _eventCount;
		public int EventCount { get { return _eventCount; } }

        /// <summary> Flag that there are changes that need saving to file </summary>
        private bool _changesPending = false;
		/// <summary> Flag that there are changes that need saving to file </summary>
        public bool ChangesPending { get { return _changesPending; } set { if (_changesPending != value) { _changesPending = value; _form.UpdateFormText(); } } }

		private bool _loading;
		/// <summary> Loading from XML is in progress </summary>
		public bool Loading { get { return _loading; } }

		/// <summary> Dependancy graph of the mission </summary>
		public DependencyGraph Dependencies { get; set; }
		public void RecalculateDependencies()
		{
			Dependencies.Recalculate(this);
		}

		public Stack<MissionSavedState> _undoStack;
		public Stack<MissionSavedState> _redoStack;

		public string UndoDescription { get { if (_undoStack.Count < 2) return null; return _undoStack.Peek().Description; } }
		public string RedoDescription { get { if (_redoStack.Count == 0) return null; return _redoStack.Peek().Description; } }

        #endregion

		#region Assigned Controls

		private TreeViewEx _nodeTV;/// TreeView control that displays a tree of nodes
		private TreeViewEx _statementTV;/// TreeView control that displays a tree of statements
		private FlowLayoutPanel _flowLP;
		private _FormMain _form;
		private TabControl _tabControl;
		private StatusStrip _statusTS;
		private ToolStripStatusLabel _objectsTotalTSSL;
		private ContextMenuStrip _labelCMS;

		public void AssignLabelCMS(ContextMenuStrip value = null)
		{
			if (_labelCMS != null)
			{
				foreach (ToolStripItem item in _labelCMS.Items)
				{
					if (item is ToolStripMenuItem)
					{
						if (((ToolStripMenuItem)item).DropDownItems.Count > 0)
						{
							foreach (ToolStripItem citem in ((ToolStripMenuItem)item).DropDownItems)
								citem.Click -= _E_l_CMS_Click;
						}
						else
						{
							item.Click -= _E_l_CMS_Click;
						}
					}
				}
			}
			_labelCMS = null;

			if (value == null)
				return;

			_labelCMS = value;
			foreach (ToolStripItem item in _labelCMS.Items)
			{
				if (item is ToolStripMenuItem)
				{
					if (((ToolStripMenuItem)item).DropDownItems.Count > 0)
					{
						foreach (ToolStripItem citem in ((ToolStripMenuItem)item).DropDownItems)
							citem.Click += _E_l_CMS_Click;
					}
					else
					{
						item.Click += _E_l_CMS_Click;
					}
				}
			}

		}
		public void AssignNodeTreeView(TreeViewEx value = null)
		{
			if (_nodeTV != null)
			{
				_nodeTV.NodesClear();
				_nodeTV.IsFolder_Reset();
				_nodeTV.IsAllowedToHaveRelation_Reset();

				_nodeTV.NodeMoved -= _E_nodeTV_NodeMoved;
				_nodeTV.AfterLabelEdit -= _E_nodeTV_AfterLabelEdit;
				_nodeTV.BeforeSelect -= _E_nodeTV_BeforeSelect;
				_nodeTV.AfterSelect -= _E_nodeTV_AfterSelect;
				_nodeTV.KeyDown -= _E_nodeTV_KeyDown;
				_nodeTV.AfterExpand -= _E_nodeTV_AfterExpand;
				_nodeTV.AfterCollapse -= _E_nodeTV_AfterCollapse;
			}
			_nodeTV = null;

			if (value == null)
				return;

			_nodeTV = value;
			_nodeTV.IsFolder = (TreeNode ii) => ii.Tag != null && ii.Tag.GetType() == typeof(MissionNode_Folder);
			_nodeTV.IsAllowedToHaveRelation = NodeIsAllowedToHaveRelation;

			_nodeTV.NodeMoved += _E_nodeTV_NodeMoved;
			_nodeTV.AfterLabelEdit += _E_nodeTV_AfterLabelEdit;
			_nodeTV.BeforeSelect += _E_nodeTV_BeforeSelect;
			_nodeTV.AfterSelect += _E_nodeTV_AfterSelect;
			_nodeTV.KeyDown += _E_nodeTV_KeyDown;
			_nodeTV.AfterExpand += _E_nodeTV_AfterExpand;
			_nodeTV.AfterCollapse += _E_nodeTV_AfterCollapse;

			//_nodeTreeView.SelectedIndexChanged += _E_namedObjectsListBox_SelectedIndexChanged;
		}
		public void AssignStatementTreeView(TreeViewEx value = null)
		{
			if (_statementTV != null)
			{
				_statementTV.NodesClear();
				_statementTV.IsFolder_Reset();
				_statementTV.IsAllowedToHaveRelation_Reset();

				_statementTV.NodeMoved -= _E_statementTV_NodeMoved;
				_statementTV.BeforeSelect -= _E_statementTV_BeforeSelect;
				_statementTV.AfterSelect -= _E_statementTV_AfterSelect;
				_statementTV.KeyDown -= _E_statementTV_KeyDown;
			}
			_statementTV = null;

			if (value == null)
				return;

			_statementTV = value;
			_statementTV.IsFolder = (TreeNode ii) => (ii.Tag is string);
			_statementTV.IsAllowedToHaveRelation = StatementIsAllowedToHaveRelation;

			_statementTV.NodeMoved += _E_statementTV_NodeMoved;
			_statementTV.BeforeSelect += _E_statementTV_BeforeSelect;
			_statementTV.AfterSelect += _E_statementTV_AfterSelect;
			_statementTV.KeyDown += _E_statementTV_KeyDown;
		}
		public void AssignFlowPanel(FlowLayoutPanel value = null)
		{
			if (_flowLP != null)
			{
				FlowLayoutPanel_Clear();
				_flowLP.Resize -= _E_flowLP_Resize;
			}
			_flowLP = null;

			if (value == null)
				return;

			_flowLP = value;
			_flowLP.Resize += _E_flowLP_Resize;
		}
		public void AssignForm(_FormMain value = null)
		{
			if (_form != null)
			{
				_form.KeyDown -= _E_form_KeyDown;
			}
			_form = null;

			if (value == null)
				return;

			_form = value;
			_form.KeyDown += _E_form_KeyDown;
		}
		public void AssignTabControl(TabControl value = null)
		{
			if (_tabControl != null)
			{
				_tabControl.TabPages[0].Text = "";
			}
			_tabControl = null;

			if (value == null)
				return;

			_tabControl = value;
		}
		public void AssignStatusToolStrip(StatusStrip value = null)
		{
			if (_objectsTotalTSSL != null)
				_objectsTotalTSSL.Text = "";

			_statusTS = null;
			_objectsTotalTSSL = null;

			if (value == null)
				return;

			_statusTS = value;

			foreach (ToolStripItem item in _statusTS.Items)
			{
				if (item.GetType() == typeof(ToolStripStatusLabel) && item.Tag != null && item.Tag.GetType() == typeof(string) && ((string)item.Tag).ToLower() == "objectstotal")
					_objectsTotalTSSL = (ToolStripStatusLabel)item;

			}
		}

		private bool NodeIsAllowedToHaveRelation(TreeNode parent, TreeNode child, int relation)
		{
			if (child.Tag == null || parent.Tag == null)
				throw new Exception("FAIL! Moving a TreeNode without a Tag, WTF?");

			if (relation == 2 && !_nodeTV.IsFolder(parent))
				return false;

			//Everything except comments cant go above Start (which can only occur in root)
			//Therefore, for each node that isnt a comment, and is trying to fit on one level with something that is in root...
			if (relation != 2 && parent.Parent == null && !(child.Tag.GetType() == typeof(MissionNode_Comment) || child.Tag.GetType() == typeof(MissionNode_Start)))
			{
				//Check that we are not inserting directly above Start node...
				if (relation == 1 && parent.Tag.GetType() == typeof(MissionNode_Start))
					return false;
				//...and check that we are not inserting near a node that is above Start node
				if (_nodeTV.Nodes.IndexOf(_startNode) > _nodeTV.Nodes.IndexOf(parent))
					return false;
			}

			//Start, Comment and Unknown cannot go inside anything, start because it must stay on top and other two because they cant have Parent_ID attribute added to them
			if (child.Tag.GetType() == typeof(MissionNode_Start) || child.Tag.GetType() == typeof(MissionNode_Comment) || child.Tag.GetType() == typeof(MissionNode_Unknown))
			{
				//Refuse to go inside anything...
				if (relation == 2)
					return false;
				//...or next to a node that is inside something...
				if (parent.Parent != null)
					return false;
			}

			//Start additionally cannot go below anything that isnt a comment
			if (child.Tag.GetType() == typeof(MissionNode_Start))
			{
				//Never go under anything other than a comment or yourself
				if (relation == 3 && parent.Tag.GetType() != typeof(MissionNode_Comment) && parent != child)
					return false;

				//Go through nodes until we meet the node we are becoming parented to
				//If we find anything except comment on the way, this means we are going below
				for (int i = 0; i < _nodeTV.Nodes.Count; i++)
				{
					//When we found something other than a comment and the dragged node
					if (_nodeTV.Nodes[i].Tag.GetType() != typeof(MissionNode_Comment) && _nodeTV.Nodes[i] != child)
						//In case this is the node we are inserting above - allow it, otherwise deny it
						if (relation == 1 && _nodeTV.Nodes[i] == parent)
							break;
						else
							return false;

					if (_nodeTV.Nodes[i] == parent)
						break;
				}
			}

			return true;
		}

		private bool StatementIsAllowedToHaveRelation(TreeNode parent, TreeNode child, int relation)
		{
			if (child.Tag == null || parent.Tag == null) // This might be a comment or a folder
				return false;

			//You cannot go inside something that isnt a folder
			if (relation == 2 && !_statementTV.IsFolder(parent))
				return false;

			//Non-statements cannot move at all
			if (!(child.Tag is MissionStatement))
				return false;

			//If going inside, Conditions can only go inside codintion folder, actions can go inside actions folder, comment can go anywhere
			if (relation == 2 && (((string)parent.Tag == "conditions" && ((MissionStatement)child.Tag).Kind == MissionStatementKind.Action) || ((string)parent.Tag == "actions" && ((MissionStatement)child.Tag).Kind == MissionStatementKind.Condition) || ((string)parent.Tag != "actions" && (string)parent.Tag != "conditions")))
				return false;

			//Comments cannot go inside conditions!
			if (relation == 2 && ((string)parent.Tag == "conditions" && ((MissionStatement)child.Tag).Kind == MissionStatementKind.Commentary))
				return false;

			//If going adjacent to something
			//Conditions can only go adjacent to conditions or comments inside conditions folder
			//Actions can only go adjacent to actions or comments inside actions folder
			if (relation == 1 || relation == 3)
			{
				if (!(parent.Tag is MissionStatement))
					return false;

				if ((((MissionStatement)child.Tag).Kind == MissionStatementKind.Action) && ((string)parent.Parent.Tag != "actions"))
					return false;

				if ((((MissionStatement)child.Tag).Kind == MissionStatementKind.Condition) && ((string)parent.Parent.Tag != "conditions"))
					return false;
			}

			//Comments also cannot go under last condition
			if (relation == 3 && (((MissionStatement)child.Tag).Kind == MissionStatementKind.Commentary))
			{
				if (parent.Parent != null && parent.Parent.LastNode == parent && (string)parent.Parent.Tag == "conditions")
					return false;
			}

			return true;
		}

		private void FlowLayoutPanel_Clear()
		{
			_flowLP.Controls.Clear();
		}

		private void FlowLayoutPanel_Suspend()
		{
			_flowLP.SuspendLayout();
		}

		private void FlowLayoutPanel_Resume()
		{
			_flowLP.ResumeLayout();
		}

		#endregion
        
        public Mission()
		{
			_nodeTV = null;
			_statementTV = null;
			_flowLP = null;
			_form = null;
			_tabControl = null;
            _statusTS = null;
            _objectsTotalTSSL = null;
			_labelCMS = null;

			this.AssignFlowPanel();
			this.AssignNodeTreeView();
			this.AssignStatementTreeView();
			this.AssignForm();
			this.AssignTabControl();
            this.AssignStatusToolStrip();
			this.AssignLabelCMS();

			Dependencies = new DependencyGraph();
			
			_variables = new List<string>();
            _variableHeaders = new List<string>();
			_timers = new List<string>();
            _timerHeaders = new List<string>();
			_nameds = new Dictionary<string,List<string>>();
            _nameds.Add("anomaly", new List<string>());
            _nameds.Add("blackHole", new List<string>());
            _nameds.Add("enemy", new List<string>());
            _nameds.Add("neutral", new List<string>());
            _nameds.Add("genericMesh", new List<string>());
            _nameds.Add("player", new List<string>());
            _nameds.Add("station", new List<string>());
            _nameds.Add("monster", new List<string>());
			_nameds.Add("whale", new List<string>());
            
			_undoStack = new Stack<MissionSavedState>();
			_redoStack = new Stack<MissionSavedState>();

			_eventCount = 0;
			
            ___STATIC_E_statementTV_SuppressSelectionEvents = false;
            ___STATIC_E_nodeTV_SupressSelectionEvents = false;
			___STATIC_E_nodeTV_SupressExpandCollapseEvents = false;
            ___STATIC_Update_Counter = 0;
		}
		
		~Mission()
		{
			//this.AssignFlowPanel();
			//this.AssignNodeTreeView();
			//this.AssignStatementTreeView();
			//this.AssignForm();
			//this.AssignTabControl();
		}

		#region Undo / Redo / Change registration

		public void Undo(MissionSavedState state = null)
		{
			if (_undoStack.Count < 2)
				return;

			state = state ?? _undoStack.Peek();
			while (_redoStack.Count==0 || _redoStack.Peek() != state)
				_redoStack.Push(_undoStack.Pop());
			FromState(_undoStack.Peek());

            UpdateObjectsText();
		}

		public void Redo(MissionSavedState state = null)
		{
			if (_redoStack.Count == 0)
				return;

			state = state ?? _redoStack.Peek();
			while (_undoStack.Count == 0 || _undoStack.Peek() != state)
				_undoStack.Push(_redoStack.Pop());
			FromState(_undoStack.Peek());
            
            UpdateObjectsText();
		}
        
		/// <summary> Registers a change, adding a new state into undo stack and cleaning redo stack in the process </summary>
		public void RegisterChange(string shortDescription, bool clean = false)
		{
			Dependencies.Invalidate();

			if (string.IsNullOrWhiteSpace(shortDescription))
				shortDescription = "NO DESCRIPTION!?";
			
			ChangesPending = !clean;
			
			if (clean) _undoStack.Clear();
			
			_undoStack.Push(ToState(shortDescription));
			_redoStack.Clear();

            UpdateObjectsText();

            UpdateObjectLists();

		}

        #endregion

        #region Mission input/output (file, state, ...) and New mission creation

        public void New(bool force = false)
        {
			if (!force && ChangesPending)
			{
				DialogResult result = MessageBox.Show("Do you want to save unsaved changes?", "Artemis Mission Editor",
				MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
				if (result == DialogResult.Cancel)
					return;
				if (result == DialogResult.Yes)
					if (!Save())
						return;
			}

            Clear();

            TreeNode newTNode = new TreeNode();
            MissionNode_Start newMNode = new MissionNode_Start();
			
			string xml = "<root>" + Settings.Current.NewMissionStartBlock + "</root>";
			XmlDocument xDoc = new XmlDocument();
			try
			{
				xDoc.LoadXml(xml);

				foreach (XmlNode node in xDoc.ChildNodes[0].ChildNodes)
				{
					newMNode.Actions.Add(MissionStatement.NewFromXML(node, newMNode));
				}
			}
			catch (Exception e)
			{
				Log.Add("Problems while trying to parse new mission start node text:");
				Log.Add(e.Message);
			}

            newTNode.Text = newMNode.Name;
            newTNode.ImageIndex = newMNode.ImageIndex;
            newTNode.SelectedImageIndex = newMNode.ImageIndex;
            newTNode.Tag = newMNode;

            _nodeTV.Nodes.Add(newTNode);
            _startNode = newTNode;
            FlowLayoutPanel_Clear();

			_nodeTV.SelectedNode = newTNode;
			
			_bgNode = newTNode;

            RegisterChange("New mission created", true);
        }

        private void Clear()
        {
            FilePath = "";
            _startNode = null;
			_bgNode = null;
            _commentsAfter = "";
            _commentsBefore = "";

            BeginUpdate();

            _nodeTV.NodesClear();
            _statementTV.NodesClear();

            EndUpdate();

            _eventCount = 0;

			if (Program.FSR != null)
				Program.FSR.ClearList();

			if (Program.FMP != null)
				if (Program.FMP.Visible)
					Program.FMP.Close();
        }

        public void FromFile(string fileName)
        {
            XmlDocument xDoc;
            xDoc = new XmlDocument();

            StreamReader streamReader = new StreamReader(fileName);
            string text = streamReader.ReadToEnd();
            streamReader.Close();
            streamReader.Dispose();

            text = Helper.FixMissionXml(text);

            if (FromXml(text))
            {
                FilePath = fileName;

                _nodeTV.SelectedNode = _startNode;

                RegisterChange("Mission read from file", true);
            }
            else
            {
                New(true);
            }
        }

        public void Open(bool force = false)
		{
            if (!force && ChangesPending)
            {
				DialogResult result = MessageBox.Show("Do you want to save unsaved changes?", "Artemis Mission Editor",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
                if (result==DialogResult.Cancel)
                    return;
                if (result == DialogResult.Yes)
                    if (!Save())
                        return;
            }

			OpenFileDialog ofd = new OpenFileDialog();
			DialogResult res;
			string fileName;
			ofd.CheckFileExists = true;
			ofd.AddExtension = true;
			ofd.Multiselect = false;
			ofd.Filter = "XML Files|*.xml|All Files|*.*";
			ofd.Title = "Open mission";

            res = ofd.ShowDialog();
			fileName = ofd.FileName;
			ofd.Dispose();
			if (res != DialogResult.OK)
				return;

            FromFile(fileName);
		}

		public bool FromXml(string text, bool supressLoadingSignal = false)
		{
			XmlDocument xDoc = new XmlDocument();

			try
			{
				xDoc.LoadXml(text);
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message);

				return false;
			}

			Clear();
			_loading = !supressLoadingSignal;
			BeginUpdate();
			
			List<TreeNode> NodesToExpand = new List<TreeNode>();
			Guid? bgGuid = null;

			XmlNode root = null;
			int i;

			for (i = 0; i < xDoc.ChildNodes.Count; i++)
			{
				XmlNode item = xDoc.ChildNodes[i];
				if (item.GetType() == typeof(XmlComment))
					if (root == null)
						_commentsBefore += item.Value + "\r\n";
					else
						_commentsAfter += item.Value + "\r\n";
				if (item.Name == "mission_data")
					root = item;
			}
			if (_commentsBefore.Length >= 2 && _commentsBefore.Substring(_commentsBefore.Length - 2, 2) == "\r\n")
				_commentsBefore.Substring(0, _commentsBefore.Length - 2);
			if (_commentsAfter.Length>=2 && _commentsAfter.Substring(_commentsAfter.Length - 2, 2) == "\r\n")
				_commentsAfter.Substring(0, _commentsAfter.Length - 2);

            if (root == null)
            {
                Log.Add("No mission_data node found in specified Xml file. Mission was not loaded");
                _loading = false;
                EndUpdate();
                return false;
            }

			foreach (XmlAttribute att in root.Attributes)
			{
				Guid tmp;
				if (att.Name == "background_id_arme")
					if (Guid.TryParse(att.Value, out tmp))
						bgGuid = tmp;
			}

			foreach (XmlNode item in root.ChildNodes)
			{
				TreeNode newNode = new TreeNode();
				MissionNode newMissionNode = MissionNode.NewFromXML(item);

				newNode.Text = newMissionNode.Name;
				newNode.Tag = newMissionNode;
				newNode.ImageIndex = newMissionNode.ImageIndex;
				newNode.SelectedImageIndex = newMissionNode.ImageIndex;
				if (newMissionNode is MissionNode_Event)
					_eventCount++;

				TreeNode parentNode = newMissionNode.ParentID == null ? null : _nodeTV.FindNode((TreeNode ii) => ((MissionNode)ii.Tag).ID == newMissionNode.ParentID);

				if (parentNode != null)
					parentNode.Nodes.Add(newNode);
				else
					_nodeTV.Nodes.Add(newNode);

				if (newMissionNode.ExtraAttributes.Contains("expanded_arme"))
					NodesToExpand.Add(newNode);

				//If background id matches then remember this node
				if (newMissionNode.ID == bgGuid)
					_bgNode = newNode;
			}

			_startNode = _nodeTV.FindNode((TreeNode ii) => ii.Tag != null && ii.Tag.GetType() == typeof(MissionNode_Start));

			if (_startNode == null)
			{
				Log.Add("No start node found in the mission! Adding blank start node to the beginning of the mission");

				TreeNode newTNode = new TreeNode();
				MissionNode_Start newMNode = new MissionNode_Start();

				newTNode.Text = newMNode.Name;
				newTNode.ImageIndex = newMNode.ImageIndex;
				newTNode.SelectedImageIndex = newMNode.ImageIndex;
				newTNode.Tag = newMNode;

				_nodeTV.Nodes.Insert(0, newTNode);
				_startNode = newTNode;
			}

			if (_bgNode == null)
				_bgNode = _startNode;

			EndUpdate();
			_loading = false;
			
			//Expand nodes that are supposed to be expanded
			foreach (TreeNode node in NodesToExpand)
				node.Expand();

			return true;
		}

		public void ToXml_private_RecursivelyOut(XmlNode root, XmlDocument xDoc, TreeNode tNode, bool full)
		{
			//Add current node's XmlNode to the document
			root.AppendChild(((MissionNode)tNode.Tag).ToXml(xDoc, full));
            //Continue recursion
			for (int i = 0; i < tNode.Nodes.Count; i++)
                ToXml_private_RecursivelyOut(root, xDoc, tNode.Nodes[i], full);
		}

		public string ToXml(bool full=false)
		{
			XmlDocument xDoc;
			XmlElement root;
			XmlAttribute xAtt;

			xDoc = new XmlDocument();

			foreach (string item in _commentsBefore.Split(new string[1]{"\r\n"}, StringSplitOptions.None))
				if (!string.IsNullOrWhiteSpace(item))
					xDoc.AppendChild(xDoc.CreateComment(item));

			root = xDoc.CreateElement("mission_data");
			if (true)
			{
				xAtt = xDoc.CreateAttribute("version");
				xAtt.Value = "1.7";
				root.Attributes.Append(xAtt);
				xDoc.AppendChild(root);
			}
			if (_bgNode!=null)
			{
				xAtt = xDoc.CreateAttribute("background_id_arme");
				xAtt.Value = ((MissionNode)_bgNode.Tag).ID.ToString();
				root.Attributes.Append(xAtt);
				xDoc.AppendChild(root);
			}

			//Out the xml's!
            for (int i = 0; i < _nodeTV.Nodes.Count; i++)
                ToXml_private_RecursivelyOut(root, xDoc, _nodeTV.Nodes[i], full);

			foreach (string item in _commentsAfter.Split(new string[1] { "\r\n" }, StringSplitOptions.None))
				if (!string.IsNullOrWhiteSpace(item))
					xDoc.AppendChild(xDoc.CreateComment(item));

			//I make this look GOOD!
			StringBuilder sb = new StringBuilder();
			XmlWriterSettings settings = new XmlWriterSettings();

			settings.Indent = true;
			settings.IndentChars = "  ";
			settings.NewLineChars = "\r\n";
			settings.NewLineHandling = NewLineHandling.Replace;
			settings.OmitXmlDeclaration = true;
			using (XmlWriter writer = XmlWriter.Create(sb, settings))
			{
				xDoc.Save(writer);
			}

			return sb.ToString();
		}

		private void FromState_private_RecursiveSelectByStep(TreeNode node, int step)
		{
			foreach (TreeNode child in node.Nodes)
				FromState_private_RecursiveSelectByStep(child, step);

			FromState_private_Step++;
			if (step == FromState_private_Step)
				_nodeTV.SelectedNode = node;

		}

		private void FromState_private_RecursiveSelectByTag(TreeNode node, MissionStatement statement)
		{
			foreach (TreeNode child in node.Nodes)
				FromState_private_RecursiveSelectByTag(child, statement);

			if (!(node.Tag is MissionStatement))
				return;

			if (((MissionStatement)node.Tag) == statement)
				_statementTV.SelectedNode = node;
		}

		private int FromState_private_Step;		
		
		private void FromState(MissionSavedState state)
		{
			Dependencies.Invalidate();
			
			BeginUpdate();

			FromXml(state.Xml, true);

			ChangesPending = state.ChangesPending;
			FilePath = state.FilePath;
			_commentsBefore = state.CommentsBefore;
			_commentsAfter = state.CommentsAfter;

            //Find selected node
			if (state.SelectedNode >= 0)
			{
				FromState_private_Step = -1;
				foreach (TreeNode node in _nodeTV.Nodes)
					FromState_private_RecursiveSelectByStep(node, state.SelectedNode);
			}

			if (state.SelectedAction >= 0)
				foreach (TreeNode node in _statementTV.Nodes)
					FromState_private_RecursiveSelectByTag(node, ((MissionNode)_nodeTV.SelectedNode.Tag).Actions[state.SelectedAction]);

			if (state.SelectedCondition >= 0)
				foreach (TreeNode node in _statementTV.Nodes)
					FromState_private_RecursiveSelectByTag(node, ((MissionNode)_nodeTV.SelectedNode.Tag).Conditions[state.SelectedCondition]);

			if (state.SelectedLabel >= 0)
				_flowLP.Controls[state.SelectedLabel].Focus();

            RecalculateNodeCount();

            EndUpdate();

		}

		private bool ToState_private_RecursivelyGetSelected(TreeNode node)
		{
			foreach (TreeNode child in node.Nodes)
				if (ToState_private_RecursivelyGetSelected(child)) return true;

			ToState_private_Step++;
			if (_nodeTV.SelectedNode == node)
				return true;
			return false;
		}

		private int ToState_private_Step;		

		private MissionSavedState ToState(string shortDescription)
		{
			MissionSavedState result = new MissionSavedState();
			result.Xml = ToXml(true);
			result.Description = shortDescription;
            result.ChangesPending = ChangesPending;
			result.FilePath = FilePath;
			result.CommentsBefore = _commentsBefore;
			result.CommentsAfter = _commentsAfter;
            result.SelectedNode = -1;
			if (_nodeTV.SelectedNode!=null)
			{
				ToState_private_Step = -1;
				foreach (TreeNode node in _nodeTV.Nodes)
					if (ToState_private_RecursivelyGetSelected(node)) break;
				result.SelectedNode = ToState_private_Step;
			}

			result.SelectedAction = -1;
			result.SelectedCondition = -1;
			result.SelectedLabel = -1;
			if (_nodeTV.SelectedNode != null && _statementTV.SelectedNode != null && _statementTV.SelectedNode.Tag is MissionStatement)
			{
				MissionNode curNode = (MissionNode)_nodeTV.SelectedNode.Tag;
				MissionStatement curStatement = (MissionStatement)_statementTV.SelectedNode.Tag;
				if (curNode.Actions.Contains(curStatement))
					result.SelectedAction = curNode.Actions.IndexOf(curStatement);
				if (curNode.Conditions.Contains(curStatement))
					result.SelectedCondition = curNode.Conditions.IndexOf(curStatement);
				foreach (Control c in _flowLP.Controls)
					if (c.Focused)
						result.SelectedLabel = _flowLP.Controls.IndexOf(c);
			}

			return result;
		}

		public bool SaveAs()
		{
			SaveFileDialog sfd = new SaveFileDialog();
			DialogResult res;
			string fileName;
			sfd.AddExtension = true;
			sfd.Filter = "XML Files|*.xml|All Files|*.*";
			sfd.Title = "Save Mission As";

			res = sfd.ShowDialog();
			fileName = sfd.FileName;
			sfd.Dispose();
			if (res != DialogResult.OK)
				return false;

			return Save(fileName);
		}

		public bool Save()
		{
            if (FilePath == "")
                return SaveAs();
            else
                if (ChangesPending)
                    return Save(FilePath);
                else
                    return false;
		}

		public bool Save(string fileName, bool autosave = false)
		{
            try
            {
                StreamWriter streamWriter = new StreamWriter(fileName, false);
                streamWriter.Write(ToXml());
                streamWriter.Close();
                streamWriter.Dispose();

				if (!autosave)
				{
					FilePath = fileName;
					RegisterChange("Mission saved", true);
				}
            }
            catch (Exception e)
            {
                Log.Add("Could not save mission: "+e.Message);
                return false;
            }
            return true;
		}

        #endregion

        /// <summary> Begin update of trees: calls BeginUpdate() of both TreeViews </summary>
		private void BeginUpdate(bool suppressSelectionEvents = false)
		{
            if (___STATIC_Update_Counter++ == 0)
            {
				Helper.PasteInProgress = true;
				___STATIC_E_nodeTV_SupressExpandCollapseEvents = true;
				_nodeTV.BeginUpdate();
                _statementTV.BeginUpdate();
                ___STATIC_E_statementTV_SuppressSelectionEvents = suppressSelectionEvents;
                ___STATIC_E_nodeTV_SupressSelectionEvents = suppressSelectionEvents;
            }
		}

		/// <summary> End update of trees: calls EndUpdate() of both TreeViews </summary>
        private void EndUpdate(bool suppressSelectionEvents = false)
		{
            if (--___STATIC_Update_Counter == 0)
            {
				Helper.PasteInProgress = false;
				___STATIC_E_nodeTV_SupressExpandCollapseEvents = false;
                _nodeTV.EndUpdate();
                _statementTV.EndUpdate();
                if (suppressSelectionEvents)
                {
                    ___STATIC_E_nodeTV_SupressSelectionEvents = false;
                    OutputMissionNodeContentsToTree();
                    ___STATIC_E_statementTV_SuppressSelectionEvents = false;
					UpdateExpression();
                }
            }
		}

        #region Statement operation (delete, move, add...)

		public void StatementEnableDisable(bool enabled)
		{
			int count = 0;
			foreach (TreeNode node in _statementTV.SelectedNodes)
			{
				if (!(node.Tag is MissionStatement))
					continue;

				MissionStatement mNE = (MissionStatement)node.Tag;

				if (enabled && mNE.Kind == MissionStatementKind.Commentary)
				{
					try
					{
						XmlDocument xD = new XmlDocument();
						xD.LoadXml(mNE.Body);
						mNE.FromXml(xD.ChildNodes[0]);
						mNE.Update();
						count++;
					}
					catch
					{
					}
				}

				if (!enabled && mNE.Kind != MissionStatementKind.Commentary)
				{
					try
					{
						XmlDocument xD = new XmlDocument();
						xD.LoadXml("<!--" + mNE.ToXml(xD, true).OuterXml + "--><root/>");
						mNE.FromXml(xD.ChildNodes[0]);
						mNE.Update();
						count++;
					}
					catch
					{
					}
				}

				UpdateStatementTree();

				node.ImageIndex = mNE.ImageIndex;
				node.SelectedImageIndex = mNE.ImageIndex;
			}

			if (count > 0)
				RegisterChange((enabled ? "Enabled" : "Disabled") + " " + count + " statement(s)");
		}

		public void StatementDelete()
		{
			if (_statementTV.SelectedNode == null)
				return;

			BeginUpdate();

			foreach(TreeNode node in _statementTV.SelectedNodes.ToList())
				if (node.Tag is MissionStatement)
					_statementTV.Nodes.Remove(node);

			EndUpdate();
			
			ImportMissionNodeContentsFromStatementTree();

            RegisterChange("Deleted statement(s)");
		}

		public void StatementMoveUp()
		{
			if (_statementTV.SelectedNode == null)
				return;

			if (_statementTV.SelectedNode.PrevNode != null)
			{
				_statementTV.MoveNode(_statementTV.SelectedNode, _statementTV.SelectedNode.PrevNode, 1);
				_statementTV.Focus();
			}
		}

		public void StatementMoveDown()
		{
			if (_statementTV.SelectedNode == null)
				return;

			if (_statementTV.SelectedNode.NextNode != null)
			{
				_statementTV.MoveNode(_statementTV.SelectedNode, _statementTV.SelectedNode.NextNode, 3);
				_statementTV.Focus();
			}
		}

		public void StatementAddCommentary(bool underCursor = false, TreeNode nodeUnderCursor = null)
		{
			if (_nodeTV.SelectedNode == null)
				return;

			_statementTV.Focus();

			TreeNode newTNode = new TreeNode();
			MissionStatement newMStatement = new MissionStatement((MissionNode)_nodeTV.SelectedNode.Tag);
			newMStatement.Type = MissionStatementType.Commentary;
			newMStatement.Name = "Commentary";
			newMStatement.Body = "";
			newMStatement.Update();

			newTNode.Text = newMStatement.Text;
			newTNode.ImageIndex = newMStatement.ImageIndex;
			newTNode.SelectedImageIndex = newMStatement.ImageIndex;
			newTNode.Tag = newMStatement;

			bool needUpdate = Statement_AddUnderNode(newTNode, underCursor ? nodeUnderCursor : _statementTV.SelectedNode);
			_statementTV.SelectedNode = newTNode;

			ImportMissionNodeContentsFromStatementTree();

			if (needUpdate)
				RegisterChange("New commentary statement");
		}

		public void StatementAddCondition(bool underCursor = false, TreeNode nodeUnderCursor = null)
		{
			if (_nodeTV.SelectedNode == null)
				return;

			_statementTV.Focus();

			TreeNode newTNode = new TreeNode();
			MissionStatement newMStatement = new MissionStatement((MissionNode)_nodeTV.SelectedNode.Tag);
			newMStatement.Type = MissionStatementType.Statement;
			newMStatement.Name = "if_variable";
			newMStatement.Body = "";
			newMStatement.Update();

			newTNode.Text = newMStatement.Text;
			newTNode.ImageIndex = newMStatement.ImageIndex;
			newTNode.SelectedImageIndex = newMStatement.ImageIndex;
			newTNode.Tag = newMStatement;

			bool needUpdate = Statement_AddUnderNode(newTNode, underCursor ? nodeUnderCursor : _statementTV.SelectedNode);
			_statementTV.SelectedNode = newTNode;

			ImportMissionNodeContentsFromStatementTree();

			if (needUpdate)
                RegisterChange("New condition statement");
		}

		public void StatementAddAction(bool underCursor = false, TreeNode nodeUnderCursor = null)
		{
			if (_nodeTV.SelectedNode == null)
				return;

			_statementTV.Focus();

			TreeNode newTNode = new TreeNode();
			MissionStatement newMStatement = new MissionStatement((MissionNode)_nodeTV.SelectedNode.Tag);
			newMStatement.Type = MissionStatementType.Statement;
			newMStatement.Name = "set_variable";
			newMStatement.Body = "";
			newMStatement.Update();

			newTNode.Text = newMStatement.Text;
			newTNode.ImageIndex = newMStatement.ImageIndex;
			newTNode.SelectedImageIndex = newMStatement.ImageIndex;
			newTNode.Tag = newMStatement;

			bool needUpdate = Statement_AddUnderNode(newTNode, underCursor ? nodeUnderCursor : _statementTV.SelectedNode);
			_statementTV.SelectedNode = newTNode;

			ImportMissionNodeContentsFromStatementTree();

			if (needUpdate)
                RegisterChange("New action statement");
		}

		/// <summary>
		/// Adds one statement inside other statement, if possible, below it or then above it, if not possible
		/// </summary>
		/// <param name="toAdd">Statement to be added</param>
		/// <param name="selected">Statement that receives the added one</param>
		public bool Statement_AddUnderNode(TreeNode toAdd, TreeNode selected)
		{
			TreeNode ultimateParent = null;
			foreach (TreeNode node in _statementTV.Nodes)
			{
                if (!(node.Tag is string))
                    continue;
				if ((string)node.Tag == "conditions" && ((MissionStatement)toAdd.Tag).Kind == MissionStatementKind.Condition)
					ultimateParent = node;
				if ((string)node.Tag == "actions" && ((MissionStatement)toAdd.Tag).Kind == MissionStatementKind.Action)
					ultimateParent = node;
				if (((MissionStatement)toAdd.Tag).Kind == MissionStatementKind.Commentary)
					ultimateParent = node;
			}

			if (ultimateParent == null)
				return false;

			if (selected != null && _statementTV.IsAllowedToHaveRelation(selected, toAdd, 2))
                _statementTV.MoveNode(toAdd, selected, 2, Settings.Current.InsertNewOverElement ? 3 : 1, true);
			else if (selected != null && _statementTV.IsAllowedToHaveRelation(selected, toAdd, Settings.Current.InsertNewOverElement ? 1 : 3))
                _statementTV.MoveNode(toAdd, selected, Settings.Current.InsertNewOverElement ? 1 : 3, -1, true);
            else if (selected != null && _statementTV.IsAllowedToHaveRelation(selected, toAdd, Settings.Current.InsertNewOverElement ? 3 : 1))
                _statementTV.MoveNode(toAdd, selected, Settings.Current.InsertNewOverElement ? 3 : 1, -1, true);
			else
			{
				if (!Settings.Current.InsertNewOverElement)
					ultimateParent.Nodes.Insert(0,toAdd);
				else
					ultimateParent.Nodes.Add(toAdd);
			}
			
			return true;
		}

		public bool StatementHasSourceXml()
		{
			if (_statementTV.SelectedNode == null)
				return false;

			if (!(_statementTV.SelectedNode.Tag is MissionStatement))
				return false; 
			
			return !string.IsNullOrEmpty(((MissionStatement)_statementTV.SelectedNode.Tag).SourceXML);
		}

		private string StatementGetXml()
		{
			if (_statementTV.SelectedNode == null)
				return null;

			if (!(_statementTV.SelectedNode.Tag is MissionStatement))
				return null;

			//XmlDocument xDoc = new XmlDocument();
			//xDoc.AppendChild(((MissionStatement)_statementTV.SelectedNode.Tag).ToXml(xDoc));
			
			//StringBuilder sb = new StringBuilder();
			//XmlWriterSettings settings = new XmlWriterSettings();

			//settings.Indent = true;
			//settings.IndentChars = "  ";
			//settings.NewLineChars = "\r\n";
			//settings.NewLineHandling = NewLineHandling.Replace;
			//settings.OmitXmlDeclaration = true;
			//using (XmlWriter writer = XmlWriter.Create(sb, settings))
			//{
			//    xDoc.Save(writer);
			//}

			//return sb.ToString();

			return ((MissionStatement)_statementTV.SelectedNode.Tag).ToXml(new XmlDocument()).OuterXml;
		}
		
		public void StatementShowXml(bool source = false)
		{
			if (_statementTV.SelectedNode == null)
				return;

			if (!(_statementTV.SelectedNode.Tag is MissionStatement))
				return;

			if (source)
			{
				string result = ((MissionStatement)_statementTV.SelectedNode.Tag).SourceXML;
				if (result != null)
					MessageBox.Show(result, "Xml source of the statement");
			}
			else
			{
				string result = StatementGetXml();
				if (result != null)
					MessageBox.Show(result, "Xml code of the statement");
			}
		}

		public void StatementCopyXml()
		{
			string result = StatementGetXml();

			if (result != null) 
				Clipboard.SetText(result);
		}

		public bool StatementCopy()
		{
			if (_statementTV.SelectedNode == null)
				return false;

			string Xml = "";

			foreach (TreeNode node in _statementTV.SelectedNodes)
				if (node.Tag is MissionStatement)
					Xml += ((MissionStatement)node.Tag).ToXml(new XmlDocument(), true).OuterXml;

			if (!string.IsNullOrWhiteSpace(Xml))
				Clipboard.SetText(Xml);

			return !string.IsNullOrWhiteSpace(Xml);
		}

        
        public bool StatementPaste()
		{
			if (_nodeTV.SelectedNode==null)
				return false;
			if (!(_nodeTV.SelectedNode.Tag is MissionNode_Event || _nodeTV.SelectedNode.Tag is MissionNode_Start))
				return false;

            XmlDocument xDoc = new XmlDocument();
            try
            {
                xDoc.LoadXml("<root>" + Helper.RemoveNodes(Helper.FixMissionXml(Clipboard.GetText())) + "</root>");
            }
            catch
            {
                return false;
            }

            XmlNode root = xDoc.ChildNodes[0];

			List<MissionStatement> newStatements = new List<MissionStatement>();
            TreeNode lastNode = null;

            BeginUpdate(true);

            foreach (XmlNode childNode in root)
            {
                MissionStatement newMStatement = MissionStatement.NewFromXML(childNode, (MissionNode)_nodeTV.SelectedNode.Tag);
                newMStatement.Update();
                newStatements.Add(newMStatement);
            }

            int needUpdate = 0;

            for (int i = 0; i < newStatements.Count; i++)
            {
				int j = Settings.Current.InsertNewOverElement ? i : newStatements.Count - 1 - i;

                TreeNode newTNode = new TreeNode();
                MissionStatement newMStatement = newStatements[j];

                newTNode.Text = newMStatement.Text;
                newTNode.ImageIndex = newMStatement.ImageIndex;
                newTNode.SelectedImageIndex = newMStatement.ImageIndex;
                newTNode.Tag = newMStatement;

                lastNode = _statementTV.SelectedNode;
                needUpdate += Statement_AddUnderNode(newTNode, _statementTV.SelectedNode) ? 1 : 0;
                _statementTV.SelectedNode = lastNode;
                lastNode = newTNode;
				_statementTV.ExpandAll();
				
                ImportMissionNodeContentsFromStatementTree();
            }

            if (lastNode!=null)
                _statementTV.SelectedNode = lastNode;

            if (needUpdate > 1)
                RegisterChange("Pasted statements");
            if (needUpdate == 1)
                RegisterChange("Pasted statement");

            EndUpdate(true);

			return true;
		}

        #endregion

        /// <summary>
		/// Used when drag and drop or copypaste or move or delete or w/e operation was preformed on the Statements TreeView 
		/// and will reload MissionNode's list of statements from the control
		/// </summary>
		private void ImportMissionNodeContentsFromStatementTree()
		{
			if (_nodeTV.SelectedNode == null)
				throw new Exception("FAIL! Moving statements while selected node is null!?");
			if (!(_nodeTV.SelectedNode.Tag is MissionNode))
				throw new Exception("FAIL! Moving statements within a non-MissionNode node!");
			
			MissionNode mn = (MissionNode)_nodeTV.SelectedNode.Tag;

			mn.Actions.Clear();
			mn.Conditions.Clear();

			foreach (TreeNode node in _statementTV.Nodes)
			{
                if (!(node.Tag is string))
                    continue;

				//Read all actions from the TreeView
				if ((string)node.Tag == "actions")
					foreach (TreeNode child in node.Nodes)
						mn.Actions.Add((MissionStatement)child.Tag);

				//Read all conditions from the TreeView
				if ((string)node.Tag == "conditions")
					foreach (TreeNode child in node.Nodes)
						mn.Conditions.Add((MissionStatement)child.Tag);
			}
		}

        #region Node operation (delete, move, add...)

        private int NodeDelete_private_RecursiveCalculate(TreeNode node)
        {
            int i = 0;
            foreach (TreeNode child in node.Nodes)
                i += NodeDelete_private_RecursiveCalculate(child);

            if (node.Tag is MissionNode_Event)
                i++;
            return i;
        }

        public void NodeDelete()
		{
			if (_nodeTV.SelectedNode == null)
				return;
			
			BeginUpdate();

			foreach (TreeNode node in _nodeTV.SelectedNodes.ToList())
				if (!(node.Tag is MissionNode_Start))
				{
					_nodeTV.Nodes.Remove(node);
					if (_bgNode == node)
						_bgNode = _startNode;
				}
				else if (Settings.Current.ClearStartNodeOnDelete)
				{
					((MissionNode_Start)node.Tag).Actions.Clear();
					if (node == StartNode)
						OutputMissionNodeContentsToTree();
				}

			EndUpdate();
			
            RecalculateNodeCount();

			RegisterChange("Deleted node(s)");
		}

		public void NodeMoveUp()
		{
			if (_nodeTV.SelectedNode == null)
				return;

			if (_nodeTV.SelectedNode.PrevNode != null)
				_nodeTV.MoveNode(_nodeTV.SelectedNode, _nodeTV.SelectedNode.PrevNode, 1);
		}

		public void NodeMoveDown()
		{
			if (_nodeTV.SelectedNode == null)
				return;

			if (_nodeTV.SelectedNode.NextNode != null)
				_nodeTV.MoveNode(_nodeTV.SelectedNode, _nodeTV.SelectedNode.NextNode, 3);
		}

		public void NodeMoveIn()
		{
			if (_nodeTV.SelectedNode == null)
				return;

			if (_nodeTV.SelectedNode.PrevNode != null && _nodeTV.IsFolder(_nodeTV.SelectedNode.PrevNode) && _nodeTV.IsAllowedToHaveRelation(_nodeTV.SelectedNode.PrevNode, _nodeTV.SelectedNode, 2))
				_nodeTV.MoveNode(_nodeTV.SelectedNode, _nodeTV.SelectedNode.PrevNode, 2);
			else if (_nodeTV.SelectedNode.NextNode != null)
				_nodeTV.MoveNode(_nodeTV.SelectedNode, _nodeTV.SelectedNode.NextNode, 2);
		}

		public void NodeMoveOut()
		{
			if (_nodeTV.SelectedNode == null)
				return;

			if (_nodeTV.SelectedNode.Parent != null)
				_nodeTV.MoveNode(_nodeTV.SelectedNode, _nodeTV.SelectedNode.Parent, 3);
		}

		private string NodeGetXml()
		{
			if (_nodeTV.SelectedNode == null)
				return null;

			if (!(_nodeTV.SelectedNode.Tag is MissionNode))
				return null;

			XmlDocument xDoc = new XmlDocument();
			xDoc.AppendChild(((MissionNode)_nodeTV.SelectedNode.Tag).ToXml(xDoc));

			StringBuilder sb = new StringBuilder();
			XmlWriterSettings settings = new XmlWriterSettings();

			settings.Indent = true;
			settings.IndentChars = "  ";
			settings.NewLineChars = "\r\n";
			settings.NewLineHandling = NewLineHandling.Replace;
			settings.OmitXmlDeclaration = true;
			using (XmlWriter writer = XmlWriter.Create(sb, settings))
			{
				xDoc.Save(writer);
			}

			return sb.ToString(); 
		}
		
		public void NodeShowXml()
		{
			if (_nodeTV.SelectedNode == null)
				return;

			if (!(_nodeTV.SelectedNode.Tag is MissionNode))
				return;

			string result = NodeGetXml();

			if (result != null)
				MessageBox.Show(result, "Xml code of the Node");
		}

		public void NodeCopyXml()
		{
			string result = NodeGetXml();

			if (result != null)
				Clipboard.SetText(result);
		}

		public bool NodeCopy()
		{
			if (_nodeTV.SelectedNode == null)
				return false;

			string Xml = "";

			foreach (TreeNode node in _nodeTV.SelectedNodes)
				Xml += ((MissionNode)node.Tag).ToXml(new XmlDocument(), true).OuterXml;
			
			if (!string.IsNullOrWhiteSpace(Xml))
				Clipboard.SetText(Xml);

			return !string.IsNullOrWhiteSpace(Xml);
		}
		
		public bool NodePaste_private_RecursivelySearch(TreeNode node, Guid? ID)
		{
			foreach (TreeNode child in node.Nodes)
				if (NodePaste_private_RecursivelySearch(child, ID)) return true;

			if (((MissionNode)node.Tag).ID==ID)
				return true;
			
			return false;
		}

		public bool NodePaste_private_Exists(Guid?  ID)
		{
			foreach (TreeNode node in _nodeTV.Nodes)
				if (NodePaste_private_RecursivelySearch(node, ID)) return true;
			return false;
		}

		public bool NodePaste()
		{
			XmlDocument xDoc = new XmlDocument();
			try
			{
                xDoc.LoadXml("<root>" + Helper.FixMissionXml(Clipboard.GetText()) + "</root>");
			}
			catch (Exception e)
			{
				Log.Add("Error parsing XML: "+e.Message);
				return false;
			}

            XmlNode root = xDoc.ChildNodes[0];

			foreach (XmlNode node in root.ChildNodes)
				if (node.Name == "mission_data")
					root = node;

            List<MissionNode> newNodes = new List<MissionNode>();
            TreeNode lastNode = null;
			bool needStartUpdate = false;
			bool folderMode = false;

            BeginUpdate(true);
			
			//PASTING FOLDER
			if (root.ChildNodes.Count == 1 && root.ChildNodes[0].Name == "folder_arme")
			{
				Guid? guid = null;
				TreeNode node = null;

				//Continue if there is a node with such guid in the mission
				if ((guid = MissionNode.NewFromXML(root.ChildNodes[0]).ID) != null && (node = _nodeTV.FindNode((TreeNode x) => ((MissionNode)x.Tag).ID == guid)) != null)
				{
					folderMode = true;

					Node_AddUnderNode(Helper.TrueClone(node), _nodeTV.SelectedNode);



					RegisterChange("Pasted folder");
				}
			}
			//PASTING STATEMENTS
			if (!folderMode)
			{
				foreach (XmlNode childNode in root)
				{
					if (childNode.Name == "start")
					{
						DialogResult res = MessageBox.Show("Shall the start node contents be appended to the current start node's contents (Yes) or replace them (No)?", "Pasting start node", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

						if (res == DialogResult.Cancel)
							continue;
						if (res == DialogResult.No)
						{
							needStartUpdate = (((MissionNode)_startNode.Tag).Actions.Count > 0);
							((MissionNode)_startNode.Tag).Actions.Clear();
						}

						MissionNode newMNode = MissionNode.NewFromXML(childNode);

						foreach (MissionStatement item in newMNode.Actions)
							((MissionNode)_startNode.Tag).Actions.Add(item);

						lastNode = _startNode;

						needStartUpdate = needStartUpdate || newMNode.Actions.Count > 0;

						if (needStartUpdate)
							RegisterChange("Pasted start node");
					}
					if (childNode.Name == "event" || childNode.Name == "disabled_event" || childNode is XmlComment || childNode.Name == "folder_arme")
					{
						MissionNode newMNode = MissionNode.NewFromXML(childNode);
						newNodes.Add(newMNode);
						newMNode.ParentID = null;
						bool id_exists = NodePaste_private_Exists(newMNode.ID);
						if (id_exists)
							newMNode.ID = Guid.NewGuid();
						if (childNode.Name == "event" || childNode.Name == "disabled_event")
							_eventCount++;
					}
				}

				for (int i = 0; i < newNodes.Count; i++)
				{
					int j = !Settings.Current.InsertNewOverElement ? newNodes.Count - 1 - i : i;

					TreeNode newTNode = new TreeNode();
					MissionNode newMNode = newNodes[j];

					newTNode.Text = newMNode.Name;
					newTNode.ImageIndex = newMNode.ImageIndex;
					newTNode.SelectedImageIndex = newMNode.ImageIndex;
					newTNode.Tag = newMNode;

					lastNode = _nodeTV.SelectedNode;
					Node_AddUnderNode(newTNode, _nodeTV.SelectedNode);
					_nodeTV.SelectedNode = lastNode;
					lastNode = newTNode;
				}

				if (lastNode != null)
					_nodeTV.SelectedNode = lastNode;

				if (newNodes.Count > 1)
					RegisterChange("Pasted event nodes");
				if (newNodes.Count == 1)
					RegisterChange("Pasted event node");
			}

			EndUpdate(true);

			if (!needStartUpdate && !folderMode && newNodes.Count == 0)
				return false;
			else
				return true;
		}

		public void NodeExpandAll()
		{
			BeginUpdate();
			_nodeTV.ExpandAll();
			EndUpdate();
			if (_nodeTV.FindNode((TreeNode x) => x.Tag is MissionNode_Folder && x.Nodes.Count > 0) != null)
				RegisterChange("Expanded all folders");
		}

		public void NodeCollapseAll()
		{
			BeginUpdate();
			_nodeTV.CollapseAll();
			EndUpdate();
			if (_nodeTV.FindNode((TreeNode x) => x.Tag is MissionNode_Folder && x.Nodes.Count > 0) != null)
				RegisterChange("Collapsed all folders");
		}

        public void OutputMissionNodeContentsToTree()
		{
			BeginUpdate();

			MissionStatement mStatement = _statementTV.SelectedNode != null && _statementTV.SelectedNode.Tag is MissionStatement ? (MissionStatement)_statementTV.SelectedNode.Tag : null;

			_statementTV.NodesClear();
			FlowLayoutPanel_Clear();

			MissionNode mNode = (MissionNode)_nodeTV.SelectedNode.Tag;

			UpdateNodeTag();

			TreeNode ifs;
			TreeNode acts;
			TreeNode nNode;
			switch (mNode.GetType().ToString())
			{
				case "ArtemisMissionEditor.MissionNode_Folder":
					nNode = _statementTV.Nodes.Add("folder", mNode.ToXml(new XmlDocument()).OuterXml.Replace("&", "&&"), 3, 3);
					nNode.Tag = mNode;
					_tabControl.TabPages[0].Text = "Folder";
					break;
				case "ArtemisMissionEditor.MissionNode_Start":
					acts = _statementTV.Nodes.Add("Actions", "Actions", 1, 1);
					acts.Tag = "actions";
					foreach (MissionStatement item in mNode.Actions)
					{
						nNode = acts.Nodes.Add(item.Text, item.Text.Replace("&", "&&"), item.ImageIndex, item.ImageIndex);
						nNode.Tag = item;
					}
					break;
				case "ArtemisMissionEditor.MissionNode_Event":
					ifs = _statementTV.Nodes.Add("Conditions", "Conditions", 0, 0);
					ifs.Tag = "conditions";
					acts = _statementTV.Nodes.Add("Actions", "Actions", 1, 1);
					acts.Tag = "actions";
					foreach (MissionStatement item in mNode.Conditions)
					{
                        nNode = ifs.Nodes.Add(item.Text, item.Text.Replace("&","&&"), item.ImageIndex, item.ImageIndex);
						nNode.Tag = item;
					}
					foreach (MissionStatement item in mNode.Actions)
					{
						nNode = acts.Nodes.Add(item.Text, item.Text.Replace("&", "&&"), item.ImageIndex, item.ImageIndex);
						nNode.Tag = item;
					}
					break;
				case "ArtemisMissionEditor.MissionNode_Comment":
					nNode = _statementTV.Nodes.Add("comment", mNode.ToXml(new XmlDocument()).OuterXml.Replace("&", "&&"), 2, 2);
					nNode.Tag = mNode;
					_tabControl.TabPages[0].Text = "Commentary";
					break;
			}

			_statementTV.ExpandAll();

            if (_statementTV.Nodes.Count>0)
                _statementTV.Nodes[0].EnsureVisible();

            SelectStatement(mStatement);

			EndUpdate();
		}

		public void NodeRename()
		{
			_nodeTV.BeginEdit();
		}

		public void NodeEnableDisable(bool enabled)
		{
			int count = 0;
			foreach (TreeNode node in _nodeTV.SelectedNodes)
			{
				if (!(node.Tag is MissionNode_Event))
					continue;

				MissionNode_Event mNE = (MissionNode_Event)node.Tag;
				count += mNE.Enabled != enabled ? 1 : 0;
				mNE.Enabled = enabled;
				node.ImageIndex = mNE.ImageIndex;
				node.SelectedImageIndex = mNE.ImageIndex;
			}

			if (count > 0)
				RegisterChange((enabled ? "Enabled" : "Disabled") + " " + count + " event(s)");
		}

        /// <summary>
        /// Adds one node inside other node, if possible, or below it, if not possible
        /// </summary>
        /// <param name="toAdd">Node to be added</param>
        /// <param name="selected">Node that receives the added one</param>
        public void Node_AddUnderNode(TreeNode toAdd, TreeNode selected)
        {
			___STATIC_E_nodeTV_SupressExpandCollapseEvents = true;
            if (selected != null && _nodeTV.IsAllowedToHaveRelation(selected, toAdd, 2))
                _nodeTV.MoveNode(toAdd, selected, 2, Settings.Current.InsertNewOverElement ? 3 : 1, true);
            else if (selected != null && _nodeTV.IsAllowedToHaveRelation(selected, toAdd, Settings.Current.InsertNewOverElement ? 1 : 3))
                _nodeTV.MoveNode(toAdd, selected, Settings.Current.InsertNewOverElement ? 1 : 3, -1, true);
            else
                _nodeTV.Nodes.Add(toAdd);
			___STATIC_E_nodeTV_SupressExpandCollapseEvents = false;
        }

        public void NodeAddEvent(bool underCursor = false, TreeNode nodeUnderCursor = null)
        {
            _eventCount++;

            TreeNode newTNode = new TreeNode();
            MissionNode_Event newMNode = new MissionNode_Event();

            newTNode.Text = newMNode.Name;
            newTNode.ImageIndex = newMNode.ImageIndex;
            newTNode.SelectedImageIndex = newMNode.ImageIndex;
            newTNode.Tag = newMNode;

            Node_AddUnderNode(newTNode, underCursor ? nodeUnderCursor : _nodeTV.SelectedNode);
            _nodeTV.SelectedNode = newTNode;

            RegisterChange("New event node");
        }

        public void NodeAddCommentary(bool underCursor = false, TreeNode nodeUnderCursor = null)
        {
            TreeNode newTNode = new TreeNode();
            MissionNode_Comment newMNode = new MissionNode_Comment();

			newMNode.Name = " - - - - - - - - - - ";
			
			newTNode.Text = newMNode.Name;
            newTNode.ImageIndex = newMNode.ImageIndex;
            newTNode.SelectedImageIndex = newMNode.ImageIndex;
            newTNode.Tag = newMNode;

            Node_AddUnderNode(newTNode, underCursor ? nodeUnderCursor : _nodeTV.SelectedNode);
            _nodeTV.SelectedNode = newTNode;

            RegisterChange("New commentary node");
        }

        public void NodeAddFolder(bool underCursor = false, TreeNode nodeUnderCursor = null)
        {
            TreeNode newTNode = new TreeNode();
            MissionNode_Folder newMNode = new MissionNode_Folder();

            newTNode.Text = newMNode.Name;
            newTNode.ImageIndex = newMNode.ImageIndex;
            newTNode.SelectedImageIndex = newMNode.ImageIndex;
            newTNode.Tag = newMNode;

            Node_AddUnderNode(newTNode, underCursor ? nodeUnderCursor : _nodeTV.SelectedNode);
            _nodeTV.SelectedNode = newTNode;

            RegisterChange("New folder node");
        }
		
        #endregion

		public void Convert_CommentariesIntoNames(bool excludeMultiline = false)
		{
			BeginUpdate();
			for (int i = _nodeTV.Nodes.Count - 1; i > 0; i--)
			{
				if (_nodeTV.Nodes[i].Tag is MissionNode_Event || _nodeTV.Nodes[i].Tag is MissionNode_Folder || _nodeTV.Nodes[i].Tag is MissionNode_Start)
				{
					MissionNode mNode = (MissionNode)_nodeTV.Nodes[i].Tag;
					if (mNode.DefaultName == 0 && _nodeTV.Nodes[i - 1].Tag is MissionNode_Comment && (!excludeMultiline || i == 1 || !(_nodeTV.Nodes[i - 2].Tag is MissionNode_Comment)))
					{
						mNode.Name = ((MissionNode_Comment)_nodeTV.Nodes[i-1].Tag).Name;
						_nodeTV.Nodes[i].Text = mNode.Name;
						_nodeTV.Nodes.RemoveAt(i - 1);
						i--;
					}
				}
			}
			EndUpdate();

			RegisterChange("Converted commentaries into names");
		}

        #region Update and Recalculate

        private void RecalculateNodeCount_private_Recursive(TreeNode node)
        {
            foreach (TreeNode child in node.Nodes)
                RecalculateNodeCount_private_Recursive(child);

            if (node.Tag is MissionNode_Event)
                _eventCount = ((MissionNode_Event)node.Tag).DefaultName > _eventCount ? ((MissionNode_Event)node.Tag).DefaultName : _eventCount;
        }

        private void RecalculateNodeCount()
        {
            _eventCount = 0;
            foreach (TreeNode node in _nodeTV.Nodes)
                RecalculateNodeCount_private_Recursive(node);
        }
        
        private void UpdateObjectsText_private_RecursivelyCount(TreeNode node, ref int f, ref int e, ref int c, ref int u, ref int a, ref int co)
        {
            foreach (TreeNode child in node.Nodes)
                UpdateObjectsText_private_RecursivelyCount(child, ref f, ref e, ref c, ref u,ref a, ref co);

            if (node.Tag is MissionNode_Folder)
                f++;
            if (node.Tag is MissionNode_Event)
                e++; 
            if (node.Tag is MissionNode_Comment)
                c++;
            if (node.Tag is MissionNode_Unknown)
                u++;

            if (node.Tag is MissionNode_Event || node.Tag is MissionNode_Start)
            {
                foreach (MissionStatement statement in ((MissionNode)node.Tag).Actions)
                {
                    a += statement.Kind == MissionStatementKind.Action ? 1 : 0;
                    co += statement.Kind == MissionStatementKind.Condition ? 1 : 0;
                    c += statement.Kind == MissionStatementKind.Commentary ? 1 : 0;
                }
                foreach (MissionStatement statement in ((MissionNode)node.Tag).Conditions)
                {
                    a += statement.Kind == MissionStatementKind.Action ? 1 : 0;
                    co += statement.Kind == MissionStatementKind.Condition ? 1 : 0;
                    c += statement.Kind == MissionStatementKind.Commentary ? 1 : 0;
                }
            }

                 
        }

        /// <summary> Update TOTAL: text in the status bar </summary>
        private void UpdateObjectsText()
        {
            if (_objectsTotalTSSL == null)
                return;

            int f = 0, e = 0, c = 0, u = 0, a = 0, co = 0;
            foreach (TreeNode node in _nodeTV.Nodes)
                UpdateObjectsText_private_RecursivelyCount(node, ref f, ref e, ref c, ref u, ref a, ref co);

            _objectsTotalTSSL.Text = "Total: "+e.ToString()+" E, "+f.ToString()+" F, "+c.ToString()+" C";
            if (u > 0)
                _objectsTotalTSSL.Text += ", "+u.ToString()+" U";
            _objectsTotalTSSL.Text += " [" + co.ToString()+" CND, "+a.ToString()+" ACT]";


        }

        private void UpdateObjectLists_private_RecursivelyScan(TreeNode node)
        {
            foreach (TreeNode child in node.Nodes)
                UpdateObjectLists_private_RecursivelyScan(child);

            if (!(node.Tag is MissionNode_Start || node.Tag is MissionNode_Event))
                return;

            //Fill lists based on Conditions
            foreach (MissionStatement statement in ((MissionNode)node.Tag).Conditions)
            {
                if (statement.Kind != MissionStatementKind.Condition)
                    continue;

				if (statement.Name == "if_variable")
				{
					string var_name = statement.GetAttribute("name");
					if (var_name != null && !_variables.Contains(var_name))
						_variables.Add(var_name);
				}

				if (statement.Name == "if_timer_finished")
				{
					string var_timer = statement.GetAttribute("name");
					if (var_timer != null && !_timers.Contains(var_timer))
						_timers.Add(var_timer);
				}
            } 
            
            //Fill lists based on Actions
            foreach (MissionStatement statement in ((MissionNode)node.Tag).Actions)
            {
                if (statement.Kind != MissionStatementKind.Action)
                    continue;

                if (statement.Name=="create")
                {
                    string type = statement.GetAttribute("type");
					string named_name;
                    if (type!=null && _nameds.ContainsKey(type) && (named_name = statement.GetAttribute("name")) != null && !_nameds[type].Contains(named_name))
                        _nameds[type].Add(named_name);
                }

				if (statement.Name == "set_variable")
                {
                    string var_name = statement.GetAttribute("name");
                    if (var_name!=null && !_variables.Contains(var_name))
                        _variables.Add(var_name);
                }

				if (statement.Name == "set_timer") 
                {
                    string var_timer = statement.GetAttribute("name");
                    if (var_timer != null && !_timers.Contains(var_timer))
                        _timers.Add(var_timer);
                }
            }
        }

        private void UpdateObjectLists()
        {
            foreach (KeyValuePair<string, List<string>> kvp in _nameds)
                kvp.Value.Clear();
            _variables.Clear();
            _variableHeaders.Clear();
            _timers.Clear();
            _timerHeaders.Clear();

            foreach (TreeNode node in _nodeTV.Nodes)
                UpdateObjectLists_private_RecursivelyScan(node);

            _timers.Sort();
            _variables.Sort();

            if (_variables.Count > Settings.Current.NamesPerSubmenu)
            {
                int i;
                for (i = 0; i < _variables.Count / Settings.Current.NamesPerSubmenu; i++)
                {
                    string first = _variables[i * Settings.Current.NamesPerSubmenu];
                    //if (i == 0 || first[0] != _variables[i * Settings.Current.NamesPerSubmenu - 1][0]) first = first.Substring(0,1).ToUpper();
                    string last = _variables[(i + 1) * Settings.Current.NamesPerSubmenu - 1];
                    //if ((i + 1) * Settings.Current.NamesPerSubmenu == _variables.Count - 1 || last[0] != _variables[(i + 1) * Settings.Current.NamesPerSubmenu - 2][0]) last = last.Substring(0, 1).ToUpper();
                    _variableHeaders.Add(first + " - " + last);
                }
                //_variableHeaders.Add(_variables[i * Settings.Current.NamesPerSubmenu] + " - " + _variables[_variables.Count - 1][0]);
				if (_variables.Count - 1 >= i * Settings.Current.NamesPerSubmenu)
					_variableHeaders.Add(_variables[i * Settings.Current.NamesPerSubmenu] + " - " + _variables[_variables.Count - 1]);
            }

			if (_timers.Count > Settings.Current.NamesPerSubmenu)
			{
				int i;
				for (i = 0; i < _timers.Count / Settings.Current.NamesPerSubmenu; i++)
				{
					string first = _timers[i * Settings.Current.NamesPerSubmenu];
					//if (i == 0 || first[0] != _timers[i * Settings.Current.NamesPerSubmenu - 1][0]) first = first.Substring(0,1).ToUpper();
					string last = _timers[(i + 1) * Settings.Current.NamesPerSubmenu - 1];
					//if ((i + 1) * Settings.Current.NamesPerSubmenu == _timers.Count - 1 || last[0] != _timers[(i + 1) * Settings.Current.NamesPerSubmenu - 2][0]) last = last.Substring(0, 1).ToUpper();
					_timerHeaders.Add(first + " - " + last);
				}
				//_timerHeaders.Add(_timers[i * Settings.Current.NamesPerSubmenu] + " - " + _timers[_timers.Count - 1][0]);
				if (_timers.Count - 1 >= i * Settings.Current.NamesPerSubmenu)
					_timerHeaders.Add(_timers[i * Settings.Current.NamesPerSubmenu] + " - " + _timers[_timers.Count - 1]);
			}

			ExpressionMemberValueEditor.TimerName.InvalidateCMS();
			ExpressionMemberValueEditor.VariableName.InvalidateCMS();
			ExpressionMemberValueEditor.NamedAllName.InvalidateCMS();
			ExpressionMemberValueEditor.NamedStationName.InvalidateCMS();
        }

        /// <summary> Update the expression shown in the flow layout panel </summary>
		public void UpdateExpression()
		{
            Control activeControl = null;

			foreach (Control c in _flowLP.Controls)
				if (c.Focused)
					activeControl = c;
			
			ExpressionMemberValueDescription lastFocusedValueDescription = (activeControl != null && activeControl is NormalSelectableLabel && activeControl.Tag is ExpressionMemberContainer) ? ((ExpressionMemberContainer)activeControl.Tag).Member.ValueDescription : null;
			string lastFocusedValueName = (activeControl != null && activeControl is NormalSelectableLabel && activeControl.Tag is ExpressionMemberContainer) ? ((ExpressionMemberContainer)activeControl.Tag).Member.Name : null;
			List<NormalLabel> lVD = new List<NormalLabel>();
			List<NormalLabel> lVN = new List<NormalLabel>();

			FlowLayoutPanel_Clear();
			
			TreeNode node = _statementTV.SelectedNode;
			if (node == null || !(node.Tag is MissionStatement)) 
				return;
			MissionStatement statement = (MissionStatement)node.Tag;

            int countActive = 0;

			FlowLayoutPanel_Suspend();
			
			//TODO: Update the expression in the flowchart <-- WTF does this mean?
			foreach (ExpressionMemberContainer item in statement.Expression)
			{
				if (!item.Member.ValueDescription.IsDisplayed)
					continue;

				NormalLabel l;
				if (item.Member.ValueDescription.IsInteractive)
				{
                    l = new NormalSelectableLabel(_form);
                    l.MouseClick += _E_l_MouseClick;
					l.PreviewKeyDown += _E_l_PreviewKeyDown;
                    l.Number = ++countActive;
				}
				else
				{
					l = new NormalLabel(_form);
				}
				
				l.Tag = item;
				
				//l.BorderStyle = BorderStyle.FixedSingle;
				UpdateLabel(l);

				if (item.Member.RequiresLinebreak)
					_flowLP.SetFlowBreak(_flowLP.Controls[_flowLP.Controls.Count - 1], true);

				_flowLP.Controls.Add(l);

			}

			//Find all items that match value name or value description
			foreach (Control c in _flowLP.Controls)
			{
				if (((ExpressionMemberContainer)((NormalLabel)c).Tag).Member.Name == lastFocusedValueName)
					lVN.Add((NormalLabel)c);
				if (((ExpressionMemberContainer)((NormalLabel)c).Tag).Member.ValueDescription == lastFocusedValueDescription)
					lVD.Add((NormalLabel)c);
			}

			switch (lVD.Count)
			{
				case 0:
					//If none matches description, pick one that matches name
					if (lVN.Count > 0) lVN[0].Focus();
					break;
				case 1:
					//If only one matches the description pick it
					lVD[0].Focus();
					break;
				default:
					//If more than one matches the description but none match the name - pick first from description
					if (lVN.Count == 0)
						lVD[0].Focus();
					//If at least one matches in name ...
					else
					{
						//We need to narrow down...
						for (int i = lVN.Count - 1; i >= 0; i--)
						{
							//Remove all that match in name that do not match in description
							if (!lVD.Contains(lVN[i]))
								lVN.RemoveAt(i);
						}
						//If none of those matching in name match in description -  pick first from description
						if (lVN.Count == 0)
							lVD[0].Focus();
						//If only one matches in name and in description - pick it
						if (lVN.Count == 1)
							lVN[0].Focus();
						//If more than one matches in name and in description...
						if (lVN.Count > 1)
						{
							//We need to narrow down again
							for (int i = lVD.Count - 1; i >= 0; i--)
							{
								//Remove all that match in description but do not match in name
								if (!lVN.Contains(lVD[i]))
									lVD.RemoveAt(i);
							}
							//If at least one matches in description and in name = pick first from description
							if (lVD.Count >= 1)
								lVD[0].Focus();
							else // else pick first from those matching by name
								lVN[0].Focus();
						}
					}
					break;
			}

			
			FlowLayoutPanel_Resume();
		}
		
		private void SelectExpressionLabel(int index)
		{
			foreach (Control c in _flowLP.Controls)
			{
				if (c is NormalSelectableLabel && ((NormalSelectableLabel)c).Number == index)
				{
                    if (((NormalSelectableLabel)c).SelectedByKeyboard && c.Focused)
                        _E_l_Activated((NormalLabel)c);
                    c.Focus();
                    ((NormalSelectableLabel)c).SelectedByKeyboard = true;
					return;
				}
			}
		}

        private void UpdateStatementTree_private_Recursive(TreeNode node)
        {
            foreach (TreeNode child in node.Nodes)
                UpdateStatementTree_private_Recursive(child);

			if (!(node.Tag is MissionStatement))
				return;

            MissionStatement ms = (MissionStatement)node.Tag;
            if (ms.Invalidated)
            {
                if (ms.InvalidatedLabel)
                {
					node.Text = ms.Text.Replace("&", "&&");
                    node.SelectedImageIndex = ms.ImageIndex;
                    node.ImageIndex = ms.ImageIndex;
                }

                if (node == _statementTV.SelectedNode)
                {
                    if (ms.InvalidatedExpression)
                    {
						node.Text = ms.Text.Replace("&", "&&");
                        UpdateExpression();
                        ms.ConfirmUpdate();
                    }

                    if (ms.InvalidatedLabel)
                        foreach (Control c in _flowLP.Controls)
                            UpdateLabel((NormalLabel)c);

                }

                ms.ConfirmUpdate();
            }
        }
        
        /// <summary> Update all the statements in the statement tree and updates expression if nessecary </summary>
		public void UpdateStatementTree()
		{
			BeginUpdate();
			foreach (TreeNode node in _statementTV.Nodes)
				UpdateStatementTree_private_Recursive(node);
			EndUpdate();
		}

		/// <summary> Refreshes the label display - resetting text, width and height </summary>
		private void UpdateLabel(NormalLabel l)
		{
			Graphics g = l.CreateGraphics();
			l.Text = ((ExpressionMemberContainer)l.Tag).GetValueDisplay();
			l.SpecialMode = l is NormalSelectableLabel && ((ExpressionMemberContainer)l.Tag).IsInvalid;
				
			if (string.IsNullOrWhiteSpace(l.Text)&&l is NormalSelectableLabel) l.Text = "[]";
			l.Width = (int)Math.Round(g.MeasureString(l.Text, l.Font, 100000, StringFormat.GenericTypographic).Width + (l.Text.Length > 0 && l.Text.Substring(l.Text.Length - 1) == " " ? g.MeasureString(" ", l.Font).Width : 0) + 0.5);
			//If width is too big (label wont fit into control) 
			if (l.Width > _flowLP.Width - 23 && !((ExpressionMemberContainer)l.Tag).Member.RequiresLinebreak)
			{
				//and the text length its required to have in order to fit is bigger than zero then refit the string
				if (l.Text.Length * (_flowLP.Width - 23) / l.Width - 4 > 0)
					l.Text = l.Text.Substring(0, l.Text.Length * (_flowLP.Width - 23) / l.Width - 4) + "... ";
				//else shrink the string to one character
				else
					l.Text = l.Text.Substring(0, 1) + "... ";
				l.Width = _flowLP.Width - 23;
				if (l.Width < 0) l.Width = 0;
			}
			l.Height = (int)Math.Round(g.MeasureString(l.Text, l.Font).Height + 0.5);
			l.Invalidate();
		}

		/// <summary> Output selected node's tag to the tabpage's top text </summary>
		private void UpdateNodeTag()
		{
			_tabControl.TabPages[0].Text = ((MissionNode)_nodeTV.SelectedNode.Tag).Name;
		}

        #endregion

        public bool CanGetStatementXmlText()
		{
			if (_statementTV.SelectedNode == null)
				return false;

			if (!(_statementTV.SelectedNode.Tag is MissionStatement))
				return false;

			return true;
		}
		
		public bool CanGetNodeXmlText()
		{
			if (_nodeTV.SelectedNode == null)
				return false;

			if (!(_nodeTV.SelectedNode.Tag is MissionNode))
				return false;

			return true;
		}

        public void ShowFindForm()
        {
			Program.FFR._FFR_tc_Main.SelectedTab = Program.FFR._FFR_tc_Main.TabPages[0];
			Program.FFR.Show();

			if (Program.FSR.Visible)
				Program.FSR.BringToFront();

			Program.FFR.BringToFront();
		}

        public void ShowReplaceForm()
        {
			Program.FFR._FFR_tc_Main.SelectedTab = Program.FFR._FFR_tc_Main.TabPages[1];
			Program.FFR.Show();
            
			if (Program.FSR.Visible)
				Program.FSR.BringToFront();
				
			Program.FFR.BringToFront();
        }

		public void ShowEventDependencyForm(bool recalculate = false)
		{
			if (recalculate)
				Program.FD.OpenEventDependency(_nodeTV.SelectedNode);
			else
				Program.FD.OpenEventDependency(null);
		}

		public void ShowMissionPropertiesForm()
		{
			if (Program.FMP.Visible)
				Program.FMP.BringToFront();
			else
			{
				Program.FMP.ReadDataFromMission();
				Program.FMP.Show();
			}
		}

		/// <summary> Check for match, taking search terms in account </summary>
		public bool Find_CheckStringForMatch(string text1, MissionSearchStructure mss)
		{
			string text2 = mss.input;
			if (!mss.matchCase)
			{
				text1 = text1.ToLower();
				text2 = text2.ToLower();
			}

			if (mss.matchExact)
				return text1 == text2;
			else
				return text1.Contains(text2);
		}

        /// <summary> Checks if current selection (node and/or statement) matches the search criteria </summary>
        public bool Find_DoesCurrentSelectionMatch(MissionSearchStructure mss)
        {
            if (Find_CheckNodeForMatch(_nodeTV.SelectedNode, (MissionNode)_nodeTV.SelectedNode.Tag, -1, mss).Valid)
                return true;

			if (_statementTV.SelectedNode == null || !(_statementTV.SelectedNode.Tag is MissionStatement))
				return false;

            return Find_CheckStatementForMatch(_nodeTV.SelectedNode, (MissionStatement)_statementTV.SelectedNode.Tag, -1, -1, mss).Valid;
        }

        /// <summary> Check if the mission node matches the search criteria, return search result if it does </summary>
        public MissionSearchResult Find_CheckNodeForMatch(TreeNode node, MissionNode mNode, int curNode, MissionSearchStructure mss)
        {
            bool statisfies = false;

            //We are interested in event/start/folder nodes...
            if (mss.nodeNames && (node.Tag is MissionNode_Event || node.Tag is MissionNode_Folder || node.Tag is MissionNode_Start))
                statisfies = statisfies | Find_CheckStringForMatch(mNode.Name, mss);

            //... or commentaries
            if (mss.commentaries && node.Tag is MissionNode_Comment)
                statisfies = statisfies | Find_CheckStringForMatch(mNode.Name, mss);
                    
            if (statisfies)
                return new MissionSearchResult(curNode, 0, mNode.Name, node, null);

            return new MissionSearchResult(curNode, 0, null, node, null);
        }

        /// <summary> Check if mission statement matches the search criteria, return search result if it does </summary>
        public MissionSearchResult Find_CheckStatementForMatch(TreeNode node, MissionStatement statement, int curNode, int curStatement, MissionSearchStructure mss)
        {
            //We are interested in comments only if we are especially looking for them
            if (statement.Kind == MissionStatementKind.Commentary)
            {
                if (mss.commentaries)
                    if (Find_CheckStringForMatch(statement.Body, mss))
                        return new MissionSearchResult(curNode, curStatement, statement.Body, node, statement);

                return new MissionSearchResult(curNode, curStatement, null, node, statement);
            }
            
            bool statisfies = false;

            //Look for xml attribute names
            if (mss.xmlAttName)
                foreach (KeyValuePair<string, string> kvp in statement.GetAttributes())
                    statisfies = statisfies | Find_CheckStringForMatch(kvp.Key, mss);

            //Look for xml attribute values
            if (mss.xmlAttValue)
                foreach (KeyValuePair<string, string> kvp in statement.GetAttributes())
                    statisfies = statisfies | ((string.IsNullOrEmpty(mss.attName) || kvp.Key == mss.attName) && Find_CheckStringForMatch(kvp.Value, mss));

            //Look for statement text
            if (mss.statementText)
                statisfies = statisfies | Find_CheckStringForMatch(statement.Text, mss);

            if (statisfies)
                return new MissionSearchResult(curNode, curStatement, statement.Text, node, statement);

            return new MissionSearchResult(curNode, curStatement, null, node, statement);
        }

        /// <summary> Add the search result to the list if its not null </summary>
        public bool Find_TryAdd(List<MissionSearchResult> list, MissionSearchResult item, bool first, ref int limitNode, ref int limitStatement)
        {
            //Stopper for when coming for a second time
            bool last = limitNode == item.CurNode && limitStatement == item.CurStatement;
            
            //Remember from what to begin if in first mode
            if (first && !last && _nodeTV.SelectedNode == item.Node && (GetSelectedStatementPos() == item.CurStatement || (_statementTV.SelectedNode != null && _statementTV.SelectedNode.Tag == item.Statement)))
            {
                limitNode = item.CurNode;
                limitStatement = item.CurStatement;
                return false;
            }

            if (item.Valid && (!first || limitNode!=-1))
            {
                list.Add(item);
                return first || last;
            }
            return last;
        }
        
		public bool FindAll_private_RecursivelyFind(TreeNode node, ref int curNode, List<MissionSearchResult> list, MissionSearchStructure mss, bool forward, bool first, ref int limitNode, ref int limitStatement)
		{
            MissionNode mNode = (MissionNode)node.Tag;

            curNode += forward ? 1 : -1;
            int curStatement = forward ? 0 : mNode.Conditions.Count + mNode.Actions.Count + 1;

            for (int i = 0; i < node.Nodes.Count; i++)
                if (FindAll_private_RecursivelyFind(node.Nodes[forward ? i : node.Nodes.Count - 1 - i], ref curNode, list, mss, forward, first, ref limitNode, ref limitStatement)) return true;

            //Skip node in we are only looking in the current node and this isnt current node
            if (mss.onlyInCurrentNode && !_nodeTV.NodeIsInsideNode(node, _nodeTV.SelectedNode))
                return false;

			if (forward)
			{
				//Check if node matches our search criteria
				if (Find_TryAdd(list, Find_CheckNodeForMatch(node, mNode, curNode, mss), first, ref limitNode, ref limitStatement)) return true;

				//Then we start looking through statements
				for (int i = 0; i < mNode.Conditions.Count; i++)
					if (Find_TryAdd(list, Find_CheckStatementForMatch(node, mNode.Conditions[forward ? i : mNode.Conditions.Count - 1 - i], curNode, forward ? ++curStatement : --curStatement, mss), first, ref limitNode, ref limitStatement)) return true;
				for (int i = 0; i < mNode.Actions.Count; i++)
					if (Find_TryAdd(list, Find_CheckStatementForMatch(node, mNode.Actions[forward ? i : mNode.Actions.Count - 1 - i], curNode, forward ? ++curStatement : --curStatement, mss), first, ref limitNode, ref limitStatement)) return true;
			}
			else
			{
				//Then we start looking through statements
				for (int i = 0; i < mNode.Actions.Count; i++)
					if (Find_TryAdd(list, Find_CheckStatementForMatch(node, mNode.Actions[forward ? i : mNode.Actions.Count - 1 - i], curNode, forward ? ++curStatement : --curStatement, mss), first, ref limitNode, ref limitStatement)) return true;
				for (int i = 0; i < mNode.Conditions.Count; i++)
					if (Find_TryAdd(list, Find_CheckStatementForMatch(node, mNode.Conditions[forward ? i : mNode.Conditions.Count - 1 - i], curNode, forward ? ++curStatement : --curStatement, mss), first, ref limitNode, ref limitStatement)) return true;

				//Check if node matches our search criteria
				if (Find_TryAdd(list, Find_CheckNodeForMatch(node, mNode, curNode, mss), first, ref limitNode, ref limitStatement)) return true;
			}
			
            return false;
		}

		/// <summary>
		/// Find all items matching the criteria set in search structure and return the list of matching items
        /// Can be used to look for the first after the current too
		/// </summary>
		public List<MissionSearchResult> FindAll(MissionSearchStructure mss, bool forward = true, bool first = false)
        {
            List<MissionSearchResult> result = new List<MissionSearchResult>();

            int curNode, limitNode = -1, limitStatement = -1;

            for (int j = 0; j < 1 + (first ? 1 : 0); j++)//do twice if looking for first
            {
                curNode = forward ? 0 : GetNodeCount() + 1;
                for (int i = 0; i < _nodeTV.Nodes.Count; i++)
                    if (FindAll_private_RecursivelyFind(_nodeTV.Nodes[forward ? i : _nodeTV.Nodes.Count - 1 - i], ref curNode, result, mss, forward, first, ref limitNode, ref limitStatement)) break;
            }

            return result;
        }

		public bool HighlightErrors_private_CheckStatement(MissionStatement statement)
		{
			bool result = !statement.IsGreen();
			if (result)
				_statementTV.HighlightedTagList.Add(statement);
			return result;
		}

		public int HighlightErrors_private_RecursivelyFind(TreeNode node)
		{
			foreach (TreeNode cnode in node.Nodes)
				HighlightErrors_private_RecursivelyFind(cnode);
			
			bool error = false;
			foreach(MissionStatement statement in ((MissionNode)node.Tag).Conditions)
				error = error | HighlightErrors_private_CheckStatement(statement);

			foreach(MissionStatement statement in ((MissionNode)node.Tag).Actions)
				error = error | HighlightErrors_private_CheckStatement(statement);
		
			if (error)
				_nodeTV.HighlightedTagList.Add(node.Tag);

			return error ? 1 : 0;
		}

		public void HighlightErrors()
		{
			_nodeTV.HighlightedTagList.Clear();
			_statementTV.HighlightedTagList.Clear();

			int count = 0;

			foreach (TreeNode node in _nodeTV.Nodes)
				count += HighlightErrors_private_RecursivelyFind(node);

			Log.Add("Total "+count+" nodes with errors found.");

			_nodeTV.Invalidate();
			_statementTV.Invalidate();
		}

        public string Replace_ReplaceInString(string text1, MissionSearchStructure mss)
        {
            if (mss.matchExact)
                text1 = mss.replacement;
            else
                if (mss.matchCase)
					text1 = text1.Replace(mss.input, mss.replacement);
                else
					text1 = Helper.StringReplaceEx(text1, mss.input, mss.replacement);
            return text1;
        }

		public int Replace_InNode(TreeNode node, int curNode, List<MissionSearchResult> list, MissionSearchStructure mss)
        {
            int replacements = 0;

            //Replace in node name
			if ((mss.nodeNames || mss.commentaries) && node != null)
			{
				MissionNode mNode = (MissionNode)node.Tag;

				//We are interested in event/start/folder nodes...
				if ((mss.nodeNames && (node.Tag is MissionNode_Event || node.Tag is MissionNode_Folder || node.Tag is MissionNode_Start))
					|| mss.commentaries && node.Tag is MissionNode_Comment)
					if (Find_CheckStringForMatch(mNode.Name, mss))
					{
						mNode.Name = Replace_ReplaceInString(mNode.Name, mss);
						replacements++;
					}

				node.Text = mNode.Name;
				if (replacements>0)
					list.Add(new MissionSearchResult(curNode, 0, mNode.Name, node, null));
			}

			return replacements;
		}

		public int Replace_InStatement(TreeNode node, MissionStatement statement, int curNode, int curStatement, List<MissionSearchResult> list, MissionSearchStructure mss)
		{
			int replacements = 0;

            //Replace in statement
			if (statement!=null)
            {
				//We are interested in comments only if we are especially looking for them
                if (statement.Kind == MissionStatementKind.Commentary)
                {
                    if (mss.commentaries)
                        if (Find_CheckStringForMatch(statement.Body, mss))
                        {
                            statement.Body = Replace_ReplaceInString(statement.Body, mss);
                            replacements++;
                        }
                }
                else
                    if (mss.xmlAttValue) //Look for xml attribute values
                        foreach (KeyValuePair<string, string> kvp in statement.GetAttributes())
                            if ((string.IsNullOrEmpty(mss.attName) || kvp.Key == mss.attName) && Find_CheckStringForMatch(kvp.Value, mss))
                            {
                                statement.SetAttribute(kvp.Key, Replace_ReplaceInString(kvp.Value, mss));
                                replacements++;
                            }
				statement.Update();
				if (replacements>0)
					list.Add(new MissionSearchResult(curNode, curStatement, statement.Text, node, statement));
            }

			return replacements;
        }

		/// <summary> Replace in currently selected node and statement </summary>
		public int ReplaceCurrent(MissionSearchStructure mss)
		{
			int replacements = 0;

			replacements += Replace_InNode(_nodeTV.SelectedNode, 0, new List<MissionSearchResult>(), mss);
			replacements += _statementTV.SelectedNode == null || !(_statementTV.SelectedNode.Tag is MissionStatement) ? 0 : Replace_InStatement(_nodeTV.SelectedNode, (MissionStatement)_statementTV.SelectedNode.Tag, 0, 0, new List<MissionSearchResult>(), mss);

			OutputMissionNodeContentsToTree();

			RegisterChange("Replaced '" + mss.input + "' with '" + mss.replacement + "' " + replacements.ToString() + " time(s).");

			return replacements;
		}

		public int ReplaceAll_private_RecursiveReplace(TreeNode node, ref int curNode, List<MissionSearchResult> list, MissionSearchStructure mss)
		{
			int replacements = 0;

			curNode++;
			int curStatement = 0;

			for (int i = 0; i < node.Nodes.Count; i++)
				replacements += ReplaceAll_private_RecursiveReplace(node.Nodes[i], ref curNode, list, mss);

			//Skip node in we are only looking in the current node and this isnt current node
			if (mss.onlyInCurrentNode && !_nodeTV.NodeIsInsideNode(node, _nodeTV.SelectedNode))
                return replacements;

			MissionNode mNode = (MissionNode)node.Tag;
			
			//Check if node matches our search criteria (before statements if going forward)
			replacements += Replace_InNode(node, curNode, list, mss);

			//Then we start looking through statements
			for (int i = 0; i < mNode.Conditions.Count; i++)
				replacements += Replace_InStatement(node, mNode.Conditions[i], curNode, ++curStatement, list, mss);
			for (int i = 0; i < mNode.Actions.Count; i++)
				replacements += Replace_InStatement(node, mNode.Actions[i], curNode, ++curStatement, list, mss); 

			return replacements;
		}

        public int ReplaceAll(List<MissionSearchResult> list, MissionSearchStructure mss)
        {
            BeginUpdate();

			int replacements = 0;
			int curNode = 0;

			for (int i = 0; i < _nodeTV.Nodes.Count; i++)
				replacements += ReplaceAll_private_RecursiveReplace(_nodeTV.Nodes[i], ref curNode, list, mss);

			OutputMissionNodeContentsToTree();
			
            RegisterChange("Replaced '" + mss.input + "' with '" + mss.replacement + "' " + replacements.ToString() + " time(s).");

			EndUpdate();

            return replacements;
        }

        public void SelectNode(TreeNode node)
        {
            _nodeTV.SelectedNode = node;
        }

		public TreeNode GetStatementNode_private_RecursivelySelect(TreeNode node, object tag)
		{
			TreeNode result = null;
			foreach (TreeNode child in node.Nodes)
				if ((result = GetStatementNode_private_RecursivelySelect(child, tag)) != null) return result;

			if (node.Tag == tag)
				return node;
			
			return null;
		}

		public TreeNode GetStatementNode(object tag)
		{
			if (tag == null)
				return null;

			TreeNode result = null;
			
			foreach (TreeNode node in _statementTV.Nodes)
				if ((result = GetStatementNode_private_RecursivelySelect(node, tag)) != null) return result;

			return null;
		}

        public void SelectStatement(object tag)
        {
			TreeNode node = GetStatementNode(tag);

			if (node!=null)
			{
				_statementTV.SelectedNode = node;
				_statementTV.SelectedNode.EnsureVisible();
			}
			else
            {
                _statementTV.SelectedNode = null;
                _E_statementTV_AfterSelect(null, null);
            }
        }

		public NormalLabel FindFirstPlusMinusAbleLabel()
		{
			NormalLabel l = null;
			foreach(Control c in _flowLP.Controls)
				if (c is NormalLabel)
				{
					l = (NormalLabel)c;
					ExpressionMemberContainer emc = ((ExpressionMemberContainer)c.Tag);
					if (!emc.Member.IsCheck && (emc.Member.ValueDescription.Type == ExpressionMemberValueType.VarBool || emc.Member.ValueDescription.Type == ExpressionMemberValueType.VarDouble || emc.Member.ValueDescription.Type == ExpressionMemberValueType.VarInteger))
						break;
					l = null;
				}
			return l;
		}

		public void SetSelection(List<MissionSearchResult> list = null)
		{
			_nodeTV.HighlightedTagList.Clear();
			_statementTV.HighlightedTagList.Clear();
			if (list != null)
			{
				foreach (MissionSearchResult item in list)
				{
					if (!_nodeTV.HighlightedTagList.Contains(item.Node.Tag) && item.Node.Tag != null)
						_nodeTV.HighlightedTagList.Add(item.Node.Tag);
					if (!_statementTV.HighlightedTagList.Contains(item.Statement) && item.Node.Tag != null)
						_statementTV.HighlightedTagList.Add(item.Node.Tag);
					if (!_statementTV.HighlightedTagList.Contains(item.Statement) && item.Statement != null)
						_statementTV.HighlightedTagList.Add(item.Statement);
				}
			}

			_nodeTV.Invalidate();
			_statementTV.Invalidate();
		}

		public void SetSelection(DependencyEvent precursorEvent, DependencyEvent selectedEvent, bool highlightActions)
		{
			_nodeTV.HighlightedTagList.Clear();
			_statementTV.HighlightedTagList.Clear();

			foreach (DependencyCondition condition in selectedEvent.Conditions)
			{
				DependencyPrecursor dp = condition.GetPrecursor(precursorEvent);
				if (dp != null)
				{
					if (highlightActions)
					{
						_statementTV.HighlightedTagList.Add(dp.Statement);
						if (_statementTV.SelectedNode == null)
							GetStatementNode(dp.Statement).EnsureVisible();
					}
					else
					{
						_statementTV.HighlightedTagList.Add(condition.Statement);
						if (_statementTV.SelectedNode == null)
							GetStatementNode(condition.Statement).EnsureVisible();
					}
				}
			}

			_nodeTV.Invalidate();
			_statementTV.Invalidate();
		}

		public void ConvertToComment()
		{
			bool convertEvents = false;
			bool convertFolders = false;
			int convertedCount = 0;
			
			foreach (TreeNode node in _nodeTV.SelectedNodes)
			{
				if (node.Parent != null)
					continue; 
				
				convertEvents = convertEvents || (node.Tag is MissionNode_Event && (((MissionNode_Event)node.Tag).Actions.Count > 0 || ((MissionNode_Event)node.Tag).Conditions.Count > 0));
				convertFolders = convertFolders || (node.Nodes.Count > 0);
			}

			convertEvents = convertEvents && MessageBox.Show("Selection contains non-empty events.\r\nDo you want them to be converted as well?\r\n(This will clear their contents)", "Artemis Mission Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
			convertFolders = convertFolders && MessageBox.Show("Selection contains non-empty folders.\r\nDo you want them to be converted as well?\r\n(This will clear their contents)", "Artemis Mission Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

			foreach (TreeNode node in _nodeTV.SelectedNodes.ToList())
			{
				//Node was already removed since a folder with it was converted to comment earlier in this loop
				if (_nodeTV.FindNode((TreeNode x) => x == node) == null)
					continue;
				//Cant touch start node
				if (node.Tag is MissionNode_Start)
					continue;
				//Skip nodes of already correct type
				if (node.Tag is MissionNode_Comment)
					continue;
				//Cannot convert node inside a folder to comment
				if (node.Parent != null)
					continue;
				//Skip nodes user decided not to convert
				if (!convertEvents && (node.Tag is MissionNode_Event && (((MissionNode_Event)node.Tag).Actions.Count > 0 || ((MissionNode_Event)node.Tag).Conditions.Count > 0)))
					continue;
				if (!convertFolders && node.Nodes.Count > 0)
					continue;
				
				
				MissionNode_Comment mnc = new MissionNode_Comment();
				mnc.Name = node.Text;
				node.Nodes.Clear();
				node.Tag = mnc;
				node.ImageIndex = mnc.ImageIndex;
				node.SelectedImageIndex = mnc.ImageIndex;
				
				if (node == _nodeTV.SelectedNode)
					OutputMissionNodeContentsToTree();

				convertedCount++;
			}

			if (convertedCount > 0)
				RegisterChange("Converted node(s) type to comment");
		}

		public void ConvertToEvent()
		{
			bool convertFolders = false;
			int convertedCount = 0;

			foreach (TreeNode node in _nodeTV.SelectedNodes)
			{
				if (node.Tag is MissionNode_Comment && _nodeTV.Nodes.IndexOf(_startNode) > _nodeTV.Nodes.IndexOf(node))
					continue; 
				
				convertFolders = convertFolders || (node.Nodes.Count > 0);
			}

			convertFolders = convertFolders && MessageBox.Show("Selection contains non-empty folders.\r\nDo you want them to be converted as well?\r\n(This will clear their contents)", "Artemis Mission Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

			foreach (TreeNode node in _nodeTV.SelectedNodes.ToList())
			{
				//Node was already removed since a folder with it was converted to comment earlier in this loop
				if (_nodeTV.FindNode((TreeNode x) => x == node) == null)
					continue;
				//Cant touch start node
				if (node.Tag is MissionNode_Start)
					continue;
				//Skip nodes of already correct type
				if (node.Tag is MissionNode_Event)
					continue;
				//Cannot convert comment over start node to something else
				if (node.Tag is MissionNode_Comment && _nodeTV.Nodes.IndexOf(_startNode) > _nodeTV.Nodes.IndexOf(node))
					continue;
				//Skip nodes user decided not to convert
				if (!convertFolders && node.Nodes.Count > 0)
					continue;

				MissionNode mn = (MissionNode)node.Tag;
				MissionNode_Event mne = new MissionNode_Event();
				mne.Name = node.Text;
				if (mn.ID != null)
				{
					mne.ID = mn.ID;
					mne.ParentID = mn.ParentID;
				}
				else
				{
					mne.ID = Guid.NewGuid();
					mne.ParentID = null;
				}
				node.Nodes.Clear();
				node.Tag = mne;
				node.ImageIndex = mne.ImageIndex;
				node.SelectedImageIndex = mne.ImageIndex;

				if (node == _nodeTV.SelectedNode)
					OutputMissionNodeContentsToTree();

				convertedCount++;
			}

			if (convertedCount > 0)
				RegisterChange("Converted node(s) type to event");
		}

		public void ConvertToFolder()
		{
			bool convertEvents = false;
			int convertedCount = 0;

			foreach (TreeNode node in _nodeTV.SelectedNodes)
			{
				if (node.Tag is MissionNode_Comment && _nodeTV.Nodes.IndexOf(_startNode) > _nodeTV.Nodes.IndexOf(node))
					continue; 

				convertEvents = convertEvents || (node.Tag is MissionNode_Event && (((MissionNode_Event)node.Tag).Actions.Count > 0 || ((MissionNode_Event)node.Tag).Conditions.Count > 0));
			}

			convertEvents = convertEvents && MessageBox.Show("Selection contains non-empty events.\r\nDo you want them to be converted as well?\r\n(This will clear their contents)", "Artemis Mission Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

			foreach (TreeNode node in _nodeTV.SelectedNodes.ToList())
			{
				//Cant touch start node
				if (node.Tag is MissionNode_Start)
					continue;
				//Skip nodes of already correct type
				if (node.Tag is MissionNode_Folder)
					continue;
				//Cannot convert comment over start node to something else
				if (node.Tag is MissionNode_Comment && _nodeTV.Nodes.IndexOf(_startNode) > _nodeTV.Nodes.IndexOf(node))
					continue;
				//Skip nodes user decided not to convert
				if (!convertEvents && (node.Tag is MissionNode_Event && (((MissionNode_Event)node.Tag).Actions.Count > 0 || ((MissionNode_Event)node.Tag).Conditions.Count > 0)))
					continue;

				MissionNode mn = (MissionNode)node.Tag;
				MissionNode_Folder mnf = new MissionNode_Folder();
				mnf.Name = node.Text;
				if (mn.ID != null)
				{
					mnf.ID = mn.ID;
					mnf.ParentID = mn.ParentID;
				}
				else
				{
					mnf.ID = Guid.NewGuid();
					mnf.ParentID = null;
				} 
				node.Tag = mnf;
				node.ImageIndex = mnf.ImageIndex;
				node.SelectedImageIndex = mnf.ImageIndex;

				if (node == _nodeTV.SelectedNode)
					OutputMissionNodeContentsToTree();

				convertedCount++;
			}

			if (convertedCount > 0)
				RegisterChange("Converted node(s) type to folder");
		}

        #region Space map interaction

        /// <summary>
        /// Wether or not user can "Add via space map" or "Edit on space map".
        /// Returns -1 if not, or amount of create statements in current node if yes
        /// </summary>
        /// <returns>-1 if not, or amount of create statements in current node if yes</returns>
        public int CanInvokeSpaceMapCreate(TreeNode node = null)
        {
			if (node == null)
				node = _nodeTV.SelectedNode;
            if (node == null)
                return -1;
			if (!(node.Tag is MissionNode_Event) && !(node.Tag is MissionNode_Start))
                return -1;
            int i=0;
			foreach (MissionStatement statement in ((MissionNode)node.Tag).Actions)
                i += (statement.IsCreateStatement()) ? 1 : 0;
            return i;
        }

        public void AddCreateStatementsViaSpaceMap(bool underCursor = false, TreeNode nodeUnderCursor = null)
        {
            if (CanInvokeSpaceMapCreate() == -1)
                return;

            int i = 0;
            int topmost = -1;

            MissionNode curNode = (MissionNode)_nodeTV.SelectedNode.Tag;

			XmlDocument xDoc;
			XmlNode root;
			string bgXml = "<bgInput></bgInput>";
			
			if (Settings.Current.ShowStartStatementsInBackground && _bgNode != _nodeTV.SelectedNode)
			{
				MissionNode bgNode = (MissionNode)_bgNode.Tag;

				xDoc = new XmlDocument();
				root = xDoc.CreateElement("bgInput");
				xDoc.AppendChild(root);

				for (i = 0; i < bgNode.Actions.Count; i++)
				{
					MissionStatement statement = bgNode.Actions[i];
					if (statement.IsCreateNamedStatement())
						root.AppendChild(statement.ToXml(xDoc, true));
					if (statement.IsCreateNamelessStatement())
						root.AppendChild(statement.ToXml(xDoc, true));
				}
				bgXml = xDoc.OuterXml;
			}

            TreeNode curTreeNode = underCursor ? nodeUnderCursor : _statementTV.SelectedNode;

			if (curTreeNode != null)
			{
				if (curTreeNode.Tag is string)
				{
					//What to do if actions node is selected
					if (curTreeNode.Tag.ToString() == "actions")
						topmost = -1;

					//What to do if conditions node is selected
					if (curTreeNode.Tag.ToString() == "conditions")
						topmost = -1;
				}
				if (curTreeNode.Tag is MissionStatement)
				{
					for (i = 0; i < curNode.Actions.Count && topmost == -1; i++)
						if (curNode.Actions[i] == (MissionStatement)curTreeNode.Tag)
							topmost = i + 1;
				}
			}
            
            SpaceMap result = FormSpaceMap.AddViaSpaceMap(bgXml);

            ParseSpaceMapCreateResults(new List<MissionStatement>(), topmost, result);
        }

        public void EditCreateStatementsOnSpaceMap(bool underCursor = false, TreeNode nodeUnderCursor = null)
        {

            //R. Judge.  Entire list of items added to "todelete".  Only add items to todelete than can be seen on spacemap.
			if (CanInvokeSpaceMapCreate() == 0)
			{
				AddCreateStatementsViaSpaceMap(underCursor, nodeUnderCursor);
				return;
			}
			if (CanInvokeSpaceMapCreate() ==-1)
                return;

            int i = 0;
            int topmost = -1;

            MissionNode curNode = (MissionNode)_nodeTV.SelectedNode.Tag;

            List<int> namedObjects = new List<int>();
            List<int> namelessObjects = new List<int>();
            List<MissionStatement> toDelete = new List<MissionStatement>();

			XmlDocument xDoc;
			XmlNode root;
            string editXml = "<createInput></createInput>";
            string bgXml = "<bgInput></bgInput>";
			
            xDoc = new XmlDocument();
            root = xDoc.CreateElement("createInput");
            xDoc.AppendChild(root);

            for (i = 0; i < curNode.Actions.Count;i++)
            {
                MissionStatement statement = curNode.Actions[i];
                if (statement.IsCreateNamedStatement())
                {
                    namedObjects.Add(i);
                    toDelete.Add(statement);
                    root.AppendChild(statement.ToXml(xDoc, true));
                }
                if (statement.IsCreateNamelessStatement())
                {
                    namelessObjects.Add(i);
                    toDelete.Add(statement);
                    root.AppendChild(statement.ToXml(xDoc, true));
                }
            }
			editXml = xDoc.OuterXml;

			if (Settings.Current.ShowStartStatementsInBackground && _bgNode != _nodeTV.SelectedNode)
			{
				MissionNode bgNode = (MissionNode)_bgNode.Tag;

				xDoc = new XmlDocument();
				root = xDoc.CreateElement("bgInput");
				xDoc.AppendChild(root);

				for (i = 0; i < bgNode.Actions.Count; i++)
				{
					MissionStatement statement = bgNode.Actions[i];
					if (statement.IsCreateNamedStatement())
						root.AppendChild(statement.ToXml(xDoc, true));
					if (statement.IsCreateNamelessStatement())
						root.AppendChild(statement.ToXml(xDoc, true));
				}
				 bgXml = xDoc.OuterXml;
			}

            topmost = (namedObjects.Count > 0 && (namedObjects[0] < topmost || topmost == -1)) ? namedObjects[0] : topmost;
            topmost = (namelessObjects.Count > 0 && (namelessObjects[0] < topmost || topmost == -1)) ? namelessObjects[0] : topmost;

            SpaceMap result = FormSpaceMap.EditOnSpaceMap(namedObjects, namelessObjects, editXml, bgXml);

            ParseSpaceMapCreateResults(toDelete, topmost, result);
        }

        private void ParseSpaceMapCreateResults(List<MissionStatement> toDelete, int topmost, SpaceMap result)
        {
            if (result == null)
                return;


            //R. Judge: Changes Jan 16, 2013, to band-aid up to 1.7
            //TODO: Go through Storage and remove matches to toDelete.
            //R. Judge: End changes Jan 16, 2013.
            int i;
            MissionNode curNode = (MissionNode)_nodeTV.SelectedNode.Tag;
            List<string> missingProperties = new List<string>();
            XmlDocument xDoc = new XmlDocument();

            //Target point for all objects that are not after an Imported object
            int target_point = topmost == -1 ? curNode.Actions.Count : topmost;
            
            #region Import Named objects from space map

            i = result.namedObjects.Count;

            while (result.namedIdList.Count > 0)
            {
                
                missingProperties.Clear();
                //R. Judge: Changes Jan 16, 2013, to band-aid up to 1.7
               
                
                int position = result.namedIdList[result.namedIdList.Count - 1];

                if (result.UnMappableStorage != null && result.UnMappableStorage.ContainsKey(position))
                {
                    curNode.Actions.Insert(position, MissionStatement.NewFromXML(result.UnMappableStorage[position], curNode));
                    result.namedIdList.RemoveAt(result.namedIdList.Count - 1);
                }
                else
                {
                    i--;
                    curNode.Actions.Insert(position,
                        MissionStatement.NewFromXML(result.namedObjects[i].ToXml(xDoc, missingProperties), curNode));

                    if (missingProperties.Count > 0 && Settings.Current.AddFailureComments)
                    {
                        curNode.Actions.Insert(result.namedIdList[result.namedIdList.Count - 1],
                        MissionStatement.NewFromXML(xDoc.CreateComment("will fail because it lacks " + missingProperties.Aggregate((x, y) => x + ", " + y) + " "), curNode));
                    }
                    if (result.namedObjects[i].Imported)
                        result.namedIdList.RemoveAt(result.namedIdList.Count - 1);
                }
            }

            while (i > 0)
            {
                i--;
                missingProperties.Clear();
                curNode.Actions.Insert(target_point,
                    MissionStatement.NewFromXML(result.namedObjects[i].ToXml(xDoc, missingProperties), curNode));

                if (missingProperties.Count > 0 && Settings.Current.AddFailureComments)
                {
                    curNode.Actions.Insert(target_point,
                    MissionStatement.NewFromXML(xDoc.CreateComment("will fail because it lacks " + missingProperties.Aggregate((x, y) => x + ", " + y) + " "), curNode));
                }
            }

            #endregion

            #region Import nameless objects from space map

            i = result.namelessObjects.Count;

            while (result.namelessIdList.Count > 0)
            {



                missingProperties.Clear();


                int position = result.namelessIdList[result.namelessIdList.Count - 1];

                if (result.UnMappableStorage != null && result.UnMappableStorage.ContainsKey(position))
                {
                    curNode.Actions.Insert(position, MissionStatement.NewFromXML(result.UnMappableStorage[position], curNode));
                    result.namelessIdList.RemoveAt(result.namelessIdList.Count - 1);
                }
                else
                {
                    i--;
                    curNode.Actions.Insert(result.namelessIdList[result.namelessIdList.Count - 1],
                        MissionStatement.NewFromXML(result.namelessObjects[i].ToXml(xDoc, missingProperties), curNode));

                    if (missingProperties.Count > 0 && Settings.Current.AddFailureComments)
                    {
                        curNode.Actions.Insert(result.namelessIdList[result.namelessIdList.Count - 1],
                        MissionStatement.NewFromXML(xDoc.CreateComment("will fail because it lacks " + missingProperties.Aggregate((x, y) => x + ", " + y) + " "), curNode));
                    }
                    if (result.namelessObjects[i].Imported)
                        result.namelessIdList.RemoveAt(result.namelessIdList.Count - 1);
                }

            }

            while (i > 0)
            {
                i--;
                missingProperties.Clear();
                curNode.Actions.Insert(target_point,
                    MissionStatement.NewFromXML(result.namelessObjects[i].ToXml(xDoc, missingProperties), curNode));

                if (missingProperties.Count > 0 && Settings.Current.AddFailureComments)
                {
                    curNode.Actions.Insert(target_point,
                    MissionStatement.NewFromXML(xDoc.CreateComment("will fail because it lacks " + missingProperties.Aggregate((x, y) => x + ", " + y) + " "), curNode));
                }
            }
            
            #endregion

            foreach (MissionStatement statement in toDelete)
                curNode.Actions.Remove(statement);

            OutputMissionNodeContentsToTree();
            RegisterChange("Changes to create statements via space map");
        }

        /// <summary> Wether or not user can "Edit statement on space map" </summary>
        public bool CanInvokeSpaceMapStatement()
        {
            if (_nodeTV.SelectedNode == null || _statementTV.SelectedNode == null)
                return false;
            if (!(_nodeTV.SelectedNode.Tag is MissionNode_Event) && !(_nodeTV.SelectedNode.Tag is MissionNode_Start))
                return false;
            if (!(_statementTV.SelectedNode.Tag is MissionStatement))
                return false;

            return (((MissionStatement)_statementTV.SelectedNode.Tag).IsSpaceMapEditableStatement());
        }

        public void EditStatementOnSpaceMap()
        {
            if (!CanInvokeSpaceMapStatement())
                return;

            int i = 0;

            MissionNode curNode = (MissionNode)_nodeTV.SelectedNode.Tag;
            
            XmlDocument xDoc;
            XmlNode root;
            string statementXml = "<statementInput></statementInput>";
            string editXml = "<createInput></createInput>";
            string bgXml = "<bgInput></bgInput>";

            xDoc = new XmlDocument();
            root = xDoc.CreateElement("createInput");
            xDoc.AppendChild(root);

            for (i = 0; i < curNode.Actions.Count; i++)
            {
                MissionStatement statement = curNode.Actions[i];
                if (statement.IsCreateNamedStatement())
                    root.AppendChild(statement.ToXml(xDoc, true));
                if (statement.IsCreateNamelessStatement())
                    root.AppendChild(statement.ToXml(xDoc, true));
            }
            editXml = xDoc.OuterXml;

            xDoc = new XmlDocument();
            root = xDoc.CreateElement("statementInput");
            xDoc.AppendChild(root);

            MissionStatement curStatement = (MissionStatement)_statementTV.SelectedNode.Tag;
            root.AppendChild(curStatement.ToXml(xDoc));
            statementXml = xDoc.OuterXml;
			
            if (Settings.Current.ShowStartStatementsInBackground)
            {
				MissionNode bgNode = (MissionNode)_bgNode.Tag;

                xDoc = new XmlDocument();
                root = xDoc.CreateElement("bgInput");
                xDoc.AppendChild(root);

                for (i = 0; i < bgNode.Actions.Count; i++)
                {
                    MissionStatement statement = bgNode.Actions[i];
                    if (statement.IsCreateNamedStatement())
                        root.AppendChild(statement.ToXml(xDoc, true));
                    if (statement.IsCreateNamelessStatement())
                        root.AppendChild(statement.ToXml(xDoc, true));
                }
                bgXml = xDoc.OuterXml;
            }

            SpaceMap result = DialogSpaceMap.EditStatementOnSpaceMap(statementXml, editXml, bgXml);

            ParseSpaceMapStatementResults(new List<MissionStatement>(), curStatement, result);
        }

        private void ParseSpaceMapStatementResults(List<MissionStatement> toDelete, MissionStatement curStatement, SpaceMap result)
        {
            if (result == null)
                return;

            curStatement.FromXml(result.SelectionSpecial.ToXml(new XmlDocument()));

            curStatement.Update();

            OutputMissionNodeContentsToTree();
            RegisterChange("Changes to statement via space map");
        }

        #endregion

        #region EVENTS

        private void _E_l_Activated(NormalLabel label, bool byMouse = false, EditorActivationMode mode = EditorActivationMode.Normal)
        {
            Point where;
            if (byMouse)
                where = Cursor.Position;
            else
            {
                where = new Point();
                where.X += label.Width / 2;
                where.Y += label.Height / 2;
                where = label.PointToScreen(where);
            }
             ((ExpressionMemberContainer)label.Tag).OnClick(label, where, mode);
        }
       
        private void _E_l_MouseClick(object sender, MouseEventArgs e)
        {
			if (e.Button == MouseButtons.Left)
				_E_l_Activated((NormalLabel)sender, true);
			if (e.Button == MouseButtons.Right)
			{
				_labelCMS.Tag = sender;
				_labelCMS.Show(((NormalLabel)sender).PointToScreen(e.Location));
			}
        }

		private void _E_l_CMS_Click(object sender, EventArgs e)
		{
			string tag = (string)((ToolStripItem)sender).Tag;
			switch (tag)
			{
				case "edit":
					_E_l_Activated((NormalLabel)_labelCMS.Tag);
					break;
				case "edit_dialog":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.ForceGUI);
					break;
				case "edit_next":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.NextMenuItem);
					break;
				case "edit_previous":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.PreviousMenuItem);
					break;
				case "edit_-1000":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Minus1000);
					break;
				case "edit_-100":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Minus100);
					break;
				case "edit_-10":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Minus10);
					break;
				case "edit_-1":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Minus);
					break;
				case "edit_-01":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Minus01);
					break;
				case "edit_+01":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Plus01);
					break;
				case "edit_+1":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Plus);
					break;
				case "edit_+10":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Plus10);
					break;
				case "edit_+100":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Plus100);
					break;
				case "edit_+1000":
					_E_l_Activated((NormalLabel)_labelCMS.Tag, false, EditorActivationMode.Plus1000);
					break;
				case "edit_space":
					EditStatementOnSpaceMap();
					break;
			}
		}

		private void _E_l_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Back to Statement tree
			if (e.KeyData == (Keys.Enter | Keys.Shift))
			{
				e.IsInputKey = true;
				_statementTV.Focus();
			}
            //Label activation
            if (e.KeyData == Keys.Enter || e.KeyData == Keys.F2)
            {
                e.IsInputKey = true;
                _E_l_Activated((NormalLabel)sender);
            }
            if (e.KeyData == (Keys.Enter | Keys.Control))
            {
                e.IsInputKey = true;
                _E_l_Activated((NormalLabel)sender, false, EditorActivationMode.ForceGUI);
            }
			if (e.KeyData == (Keys.F2 | Keys.Control))
			{
				e.IsInputKey = true;
				EditStatementOnSpaceMap();
			}
            if (e.KeyData == Keys.Space)
            {
                e.IsInputKey = true;
                _E_l_Activated((NormalLabel)sender, false, EditorActivationMode.NextMenuItem);
            }
			if (e.KeyData == (Keys.Space | Keys.Shift))
			{
				e.IsInputKey = true;
				_E_l_Activated((NormalLabel)sender, false, EditorActivationMode.PreviousMenuItem);
			}
			if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract)
			{
				e.IsInputKey = true;
				
				EditorActivationMode mode = EditorActivationMode.Minus;
				mode = Control.ModifierKeys == (Keys.Alt)					? EditorActivationMode.Minus01		: mode;
				mode = Control.ModifierKeys == (Keys.Shift)					? EditorActivationMode.Minus10		: mode;
				mode = Control.ModifierKeys == (Keys.Control)				? EditorActivationMode.Minus100		: mode;
				mode = Control.ModifierKeys == (Keys.Shift | Keys.Control)	? EditorActivationMode.Minus1000	: mode;

				_E_l_Activated((NormalLabel)sender, false, mode);
			}
			if (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
			{
				e.IsInputKey = true;
				
				EditorActivationMode mode = EditorActivationMode.Plus;
				mode = Control.ModifierKeys == (Keys.Alt)					? EditorActivationMode.Plus01		: mode;
				mode = Control.ModifierKeys == (Keys.Shift)					? EditorActivationMode.Plus10		: mode;
				mode = Control.ModifierKeys == (Keys.Control)				? EditorActivationMode.Plus100		: mode;
				mode = Control.ModifierKeys == (Keys.Shift | Keys.Control)	? EditorActivationMode.Plus1000		: mode;

				_E_l_Activated((NormalLabel)sender, false, mode);
			}

			if (!e.IsInputKey)
			{
				_E_statementTV_KeyDown(sender, new KeyEventArgs(e.KeyData));
			}
        }
                
        private void _E_statementTV_BeforeSelect(object sender, TreeViewCancelEventArgs e)
		{
			//TODO: Prevent selection of other member if there are errors (or maybe not?)
		}

		public void _E_statementTV_AfterSelect(object sender, TreeViewEventArgs e)
		{
            if (!___STATIC_E_statementTV_SuppressSelectionEvents)
                UpdateExpression();
		}

        private void _E_statementTV_NodeMoved(TreeNode node, bool suspendUpdate)
		{
			ImportMissionNodeContentsFromStatementTree();

            if (!suspendUpdate)
			    RegisterChange("Statement moved");
		}
		
		private void _E_nodeTV_BeforeSelect(object sender, TreeViewCancelEventArgs e)
		{
			//TODO: Prevent selection of other member if there are errors (or maybe not?)
		}

		private void _E_nodeTV_AfterSelect(object sender, TreeViewEventArgs e)
		{
            if (!___STATIC_E_nodeTV_SupressSelectionEvents)
                OutputMissionNodeContentsToTree();
		}

		private void _E_nodeTV_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
		{
			if (e.Label == null)
				return;
			MissionNode mNode = (MissionNode)e.Node.Tag;
			string prevName = mNode.Name;
			try
			{
				mNode.Name = e.Label;
				string tmp = mNode.ToXml(new XmlDocument()).OuterXml;
			}
			catch (Exception ee)
			{
				MessageBox.Show(ee.Message,"Bad node name!");
				mNode.Name = prevName;
				e.CancelEdit = true;
				return;
			}
			UpdateNodeTag();
			if (mNode is MissionNode_Folder || mNode is MissionNode_Comment)
				OutputMissionNodeContentsToTree();

			RegisterChange("Node label edited");
		}

        private void _E_nodeTV_NodeMoved(TreeNode node, bool suspendUpdate)
		{
			if (node == null)
				return;

			//Assign parentship
			if (node.Parent != null)
				((MissionNode)node.Tag).ParentID = ((MissionNode)node.Parent.Tag).ID;
			else
				((MissionNode)node.Tag).ParentID = null;

			//Adjust start node
			if (_startNode.Tag == node.Tag)
				_startNode = node;

            if (!suspendUpdate)
			    RegisterChange("Node moved");
		}

		private void _E_nodeTV_AfterExpand(object sender, TreeViewEventArgs e)
		{
			MissionNode mNode = ((MissionNode)e.Node.Tag);
			if (!mNode.ExtraAttributes.Contains("expanded_arme"))
			{
				mNode.ExtraAttributes.Add("expanded_arme");
				if (!___STATIC_E_nodeTV_SupressExpandCollapseEvents && !_nodeTV.DraggingInProgress)
					RegisterChange("Expanded folder");
			}
		}
		private void _E_nodeTV_AfterCollapse(object sender, TreeViewEventArgs e)
		{
			MissionNode mNode = ((MissionNode)e.Node.Tag);
			if (mNode.ExtraAttributes.Contains("expanded_arme"))
			{
				mNode.ExtraAttributes.Remove("expanded_arme");
				if (!___STATIC_E_nodeTV_SupressExpandCollapseEvents && !_nodeTV.DraggingInProgress)
					RegisterChange("Collapsed folder");
			}
		}

		private void _E_form_KeyDown(object sender, KeyEventArgs e)
		{
			//File commands
            if (e.KeyData == (Keys.N | Keys.Control))
            {
                e.SuppressKeyPress = true;
                _form.BeginInvoke(new Action(() =>
				New()
				));
            } 
            if (e.KeyData == (Keys.O | Keys.Control))
			{
				e.SuppressKeyPress = true;
				_form.BeginInvoke(new Action(() =>
				Open()
				));
			}
            if (e.KeyData == (Keys.S | Keys.Control))
			{
				e.SuppressKeyPress = true;
				_form.BeginInvoke(new Action(() =>
				Save()
				));
			}
            if (e.KeyData == (Keys.S | Keys.Control | Keys.Alt))
            {
                e.SuppressKeyPress = true;
				_form.BeginInvoke(new Action(() =>
				SaveAs()
				));
            }

			//Edit commands
			if (e.KeyData == (Keys.Z | Keys.Control))
			{
				e.SuppressKeyPress = true;
				Undo();
            }
            if (e.KeyData == (Keys.Y | Keys.Control))
            {
                e.SuppressKeyPress = true;
                Redo();
            }
			if (e.KeyData == (Keys.F | Keys.Control) || e.KeyData == (Keys.F | Keys.Control | Keys.Shift))
			{
				e.SuppressKeyPress = true;
				ShowFindForm();
			}
			if (e.KeyData == (Keys.H | Keys.Control) || e.KeyData == (Keys.H | Keys.Control | Keys.Shift))
            {
                e.SuppressKeyPress = true;
                ShowReplaceForm();
            }
            if (e.KeyData == Keys.F3 )
            {
                e.SuppressKeyPress = true;
                Program.FFR.FindNext();
            }
            if (e.KeyData == (Keys.F3 | Keys.Shift))
            {
                e.SuppressKeyPress = true;
                Program.FFR.FindPrevious();
            }
            if (e.KeyData == Keys.F4)
            {
                e.SuppressKeyPress = true;
				ShowEventDependencyForm(true);
            }
            if (e.KeyData == (Keys.F4 | Keys.Shift))
            {
                e.SuppressKeyPress = true;
				ShowEventDependencyForm();
            }
			if (e.KeyData == (Keys.P | Keys.Control))
			{
				e.SuppressKeyPress = true;
				ShowMissionPropertiesForm();
			}
            //Label selection keys
			if (e.KeyCode == Keys.D1)
			{
				SelectExpressionLabel(1);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D2)
			{
				SelectExpressionLabel(2);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D3)
			{
				SelectExpressionLabel(3);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D4)
			{
				SelectExpressionLabel(4);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D5)
			{
				SelectExpressionLabel(5);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D6)
			{
				SelectExpressionLabel(6);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D7)
			{
				SelectExpressionLabel(7);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D8)
			{
				SelectExpressionLabel(8);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D9)
			{
				SelectExpressionLabel(9);
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.D0)
			{
				SelectExpressionLabel(10);
				e.SuppressKeyPress = true;
			}

			//Expand/collapse
			if (e.KeyData == (Keys.E | Keys.Control))
			{
				e.SuppressKeyPress = true;
				NodeExpandAll();
			}
			if (e.KeyData == (Keys.R | Keys.Control))
			{
				e.SuppressKeyPress = true;
				NodeCollapseAll();
			}
		}

		private void _E_nodeTV_KeyDown(object sender, KeyEventArgs e)
		{
			//Forward to Statement tree
			if (e.KeyCode == Keys.Enter && Control.ModifierKeys == Keys.None)
			{
				_statementTV.Focus();
				e.SuppressKeyPress = true;
			}

			//Edit menu keys
			if (e.KeyData == (Keys.C | Keys.Control) || e.KeyData == (Keys.Insert | Keys.Control))
			{
				if (NodeCopy())
					e.SuppressKeyPress = true;
			}
			if (e.KeyData == (Keys.X | Keys.Control) || e.KeyData == (Keys.Delete | Keys.Shift))
			{
				if (NodeCopy())
				{
					e.SuppressKeyPress = true;
					NodeDelete();
				}
			}
			if (e.KeyData == (Keys.V | Keys.Control) || e.KeyData == (Keys.Insert | Keys.Shift))
			{
				e.SuppressKeyPress = true;

				if (!NodePaste() && StatementPaste() && Settings.Current.FocusOnStatementPaste)
				{
					_statementTV.Focus();
				}
			}
			//CMS keys
			if (e.KeyCode == Keys.Up && Control.ModifierKeys == Keys.Control)
			{
				NodeMoveUp();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Down && Control.ModifierKeys == Keys.Control)
			{
				NodeMoveDown();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Right && Control.ModifierKeys == Keys.Control)
			{
				NodeMoveIn();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Left && Control.ModifierKeys == Keys.Control)
			{
				NodeMoveOut();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Delete && Control.ModifierKeys == Keys.None)
			{
				NodeDelete();
				e.SuppressKeyPress = true;
			}
			if (e.KeyData == (Keys.Control | Keys.D))
			{
				e.SuppressKeyPress = true;
				NodeEnableDisable(false);
			}
			if (e.KeyData == (Keys.Control |Keys.Shift | Keys.D))
			{
				e.SuppressKeyPress = true;
				NodeEnableDisable(true);
			}
			if (e.KeyCode == Keys.F2 && Control.ModifierKeys == Keys.None)
			{
				NodeRename();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Insert && Control.ModifierKeys == Keys.None)
			{
				NodeAddEvent();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Insert && Control.ModifierKeys == Keys.Alt)
			{
				NodeAddFolder();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Insert && Control.ModifierKeys == (Keys.Control | Keys.Shift))
			{
				NodeAddCommentary();
				e.SuppressKeyPress = true;
			}

			//Catch events that will DING
            if (e.KeyData == (Keys.Control | Keys.F) || e.KeyData == (Keys.Control | Keys.H))
			{
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		private void _E_statementTV_KeyDown(object sender, KeyEventArgs e)
		{
			//Forward to Flow layout panel
			if (e.KeyCode == Keys.Enter && Control.ModifierKeys == Keys.None)
			{
				e.SuppressKeyPress = true;
				SelectExpressionLabel(1);
			}

			//Back to Node tree
			if (e.KeyCode == Keys.Enter && Control.ModifierKeys == Keys.Shift)
			{
				e.SuppressKeyPress = true;
				_nodeTV.Focus();
			}

            //Edit menu keys
			if (e.KeyData == (Keys.C | Keys.Control) || e.KeyData == (Keys.Insert | Keys.Control))
			{
				if (StatementCopy())
					e.SuppressKeyPress = true;
			}
			if (e.KeyData == (Keys.X | Keys.Control) || e.KeyData == (Keys.Delete | Keys.Shift))
			{
				if (StatementCopy())
				{
					e.SuppressKeyPress = true;
					StatementDelete();
				}
			}
			if (e.KeyData == (Keys.V | Keys.Control) || e.KeyData == (Keys.Insert | Keys.Shift))
			{
				e.SuppressKeyPress = true;
				StatementPaste();
				_statementTV.Focus();
			}
            //EDIT keys
            if (e.KeyData == Keys.F2)
            {
				e.SuppressKeyPress = true;
				EditCreateStatementsOnSpaceMap();
            }
            if (e.KeyData == (Keys.F2 | Keys.Shift))
            {
				e.SuppressKeyPress = true;
				AddCreateStatementsViaSpaceMap();
            }
            if (e.KeyData == (Keys.F2 | Keys.Control))
            {
				e.SuppressKeyPress = true;
				EditStatementOnSpaceMap();
            }
			if (e.KeyData == (Keys.Control | Keys.D))
			{
				e.SuppressKeyPress = true;
				StatementEnableDisable(false);
			}
			if (e.KeyData == (Keys.Control | Keys.Shift | Keys.D))
			{
				e.SuppressKeyPress = true;
				StatementEnableDisable(true);
			}
			
			//CMS keys
			if (e.KeyCode == Keys.Up && Control.ModifierKeys == Keys.Control)
			{
				StatementMoveUp();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Down && Control.ModifierKeys == Keys.Control)
			{
				StatementMoveDown();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Delete && Control.ModifierKeys == Keys.None)
			{
				StatementDelete();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Insert && Control.ModifierKeys == Keys.None)
			{
				StatementAddAction();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Insert && Control.ModifierKeys == Keys.Alt)
			{
				StatementAddCondition();
				e.SuppressKeyPress = true;
			}
			if (e.KeyCode == Keys.Insert && Control.ModifierKeys == (Keys.Control | Keys.Shift))
			{
				StatementAddCommentary();
				e.SuppressKeyPress = true;
			}

            //Catch events that will DING
			if (e.KeyData == (Keys.Control | Keys.F) || e.KeyData == (Keys.Shift | Keys.Control | Keys.F) || e.KeyData == (Keys.Control | Keys.H) || e.KeyData == (Keys.Shift | Keys.Control | Keys.H))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

			//Catch +/-
			if (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract || e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add)
			{
				NormalLabel l = FindFirstPlusMinusAbleLabel();
				if (l != null)
				{
					if (Settings.Current.SelectLabelWhenUsingPlusMinus)
						l.Focus();
					e.SuppressKeyPress = true;
					_E_l_PreviewKeyDown(l, new PreviewKeyDownEventArgs(e.KeyData));
				}
			}
		}

        private void _E_flowLP_Resize(object sender, EventArgs e)
		{
			foreach (Control c in _flowLP.Controls)
			{
				if (c is NormalLabel)
					UpdateLabel((NormalLabel)c);
			}

			_flowLP.PerformLayout();

			_flowLP.Invalidate(true);
		}

        #endregion

        #region Debug XML output

        private void GetDebugXmlOutput_private_GetDiff(string caption, MissionStatement statement, ref string output, ref bool first)
		{
			if (statement.Kind == MissionStatementKind.Commentary)
				return;
			if (string.IsNullOrWhiteSpace(statement.SourceXML))
				return;

			XmlDocument xDoc = new XmlDocument();
			xDoc.LoadXml(statement.SourceXML);
			XmlNode xOld = xDoc.ChildNodes[0];
			XmlNode xNew = statement.ToXml(xDoc);
			string diffs = "";

			if (xOld.Name!=xNew.Name)
				diffs+="\r\n"+"Name : <"+xOld.Name+"> => <"+xNew.Name+">";
			foreach (XmlAttribute atto in xOld.Attributes)
			{
				XmlAttribute attn = xNew.Attributes[atto.Name];
				if (attn == null)
					diffs += "\r\n" + "Att : <" + atto.Name + "> removed!";
				else if (attn.Value!=atto.Value)
					diffs += "\r\n" + "Att : <" + atto.Name + "> value changed <"+atto.Value+"> => <"+attn.Value+">";
			}
			foreach (XmlAttribute attn in xNew.Attributes)
			{
				XmlAttribute atto = xOld.Attributes[attn.Name];
				if (atto == null)
					diffs += "\r\n" + "Att : added <" + attn.Name + ">";
			}
			if (diffs != "")
			{
				if (first)
				{
					first = false;
					output += caption;
				}
				output += "\r\n" + "\r\n" + xOld.OuterXml + " => " + xNew.OuterXml;
				output += diffs;
			}
		}

		private void GetDebugXmlOutput_private_RecursivelyOutput(TreeNode node, ref string output)
		{
			foreach (TreeNode child in node.Nodes)
				GetDebugXmlOutput_private_RecursivelyOutput(child, ref output);

			if (!(node.Tag is MissionNode_Start) && !(node.Tag is MissionNode_Event))
				return;

			MissionNode curMNode = (MissionNode)node.Tag;

			bool first = true;
			string caption = "\r\n" + "\r\n" + curMNode.Name + ":";
			foreach (MissionStatement statement in curMNode.Conditions)
				GetDebugXmlOutput_private_GetDiff(caption,statement,ref  output,ref first);

			foreach (MissionStatement statement in curMNode.Actions)
				GetDebugXmlOutput_private_GetDiff(caption,statement, ref output,ref first);
		}

		public string GetDebugXmlOutput()
		{
			string output = "";
			foreach (TreeNode node in _nodeTV.Nodes)
				GetDebugXmlOutput_private_RecursivelyOutput(node, ref output);
			return output;
        }

        public int GetNodeCount_private_RecursivelyCount(TreeNode node)
        {
            int result = 0;
            foreach (TreeNode cnode in node.Nodes)
                result += GetNodeCount_private_RecursivelyCount(cnode);
            return result + 1;
        }
        
        public int GetNodeCount()
        {
            int result = 0;
            foreach (TreeNode node in _nodeTV.Nodes)
                result += GetNodeCount_private_RecursivelyCount(node);
            return result;
        }

		public void GetNodes_private_RecursivelyCount(TreeNode node, List<TreeNode> list)
		{
			list.Add(node);
			
			foreach (TreeNode cnode in node.Nodes)
				GetNodes_private_RecursivelyCount(cnode, list);
		}

		public List<TreeNode> GetNodes()
		{
			List<TreeNode> list = new List<TreeNode>();

			foreach (TreeNode node in _nodeTV.Nodes)
				GetNodes_private_RecursivelyCount(node, list);

			return list;
		}

		public TreeNode GetSelectedNode()
		{
			return _nodeTV.SelectedNode;
		}

		public int GetSelectedStatementPos()
        {
            if (_statementTV.SelectedNode == null)
                return 0;
            if (_statementTV.SelectedNode.Tag is string && (string)_statementTV.SelectedNode.Tag == "conditions")
                return 0;
            if (_statementTV.SelectedNode.Tag is string && (string)_statementTV.SelectedNode.Tag == "actions")
                return ((MissionNode)_nodeTV.SelectedNode.Tag).Conditions.Count;
            return -1;
        }

        #endregion
    }
}

