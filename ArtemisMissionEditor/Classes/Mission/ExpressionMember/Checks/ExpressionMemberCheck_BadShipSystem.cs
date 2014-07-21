﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows.Forms;

namespace ArtemisMissionEditor
{
	using EMVD = ExpressionMemberValueDescription;
	using EMVB = ExpressionMemberValueBehaviorInXml;
	using EMVT = ExpressionMemberValueType;
	using EMVE = ExpressionMemberValueEditor;

	public sealed class ExpressionMemberCheck_BadShipSystem : ExpressionMemberCheck
	{
		public override string Decide(ExpressionMemberContainer container)
		{
			if (EMVD.GetItem("systemType").Editor.ValueToDisplayList.ContainsKey(container.GetAttribute("systemType", EMVD.GetItem("systemType").DefaultIfNull)))
				return _choices[0]; // ok
			else
				return _choices[1]; // not ok
		}

		protected override void SetValueInternal(ExpressionMemberContainer container, string value)
		{
			if (Mission.Current.Loading && value == "!ok")
				Log.Add("Warning! Unknown ship system " + container.GetAttribute("systemType") + " detected in event: " + container.ParentStatement.Parent.Name + "!");
			
			base.SetValueInternal(container, value);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="prefix"></param>
		/// <param name="pointCaption"></param>
		public ExpressionMemberCheck_BadShipSystem()
			: base("", EMVD.GetItem("<label>"))
		{
			List<ExpressionMember> eML;

			eML = this.Add("ok"); //_choices[0]
			eML.Add(new ExpressionMember(""));

			eML = this.Add("!ok"); //_choices[1]
			eML.Add(new ExpressionMember("(WARNING! UNKNOWN SHIP SYSTEM NAME)"));
		}
	}
}