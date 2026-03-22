using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
using System.Linq;
using TSMapEditor.Models;

namespace TSMapEditor.UI.Windows
{
    public class SelectTeamTypeWindow : SelectObjectWindow<TeamType>
    {
        public SelectTeamTypeWindow(WindowManager windowManager, Map map) : base(windowManager)
        {
            this.map = map;
        }

        private readonly Map map;

        public bool IncludeNone { get; set; }

        public bool IsForSecondaryTeam { get; set; }

        private XNACheckBox chkIncludeGlobalTeamTypes;

        public override void Initialize()
        {
            Name = nameof(SelectTeamTypeWindow);
            base.Initialize();

            chkIncludeGlobalTeamTypes = FindChild<XNACheckBox>(nameof(chkIncludeGlobalTeamTypes));
            chkIncludeGlobalTeamTypes.CheckedChanged += (_, _) => ListObjects();
        }

        protected override void LbObjectList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lbObjectList.SelectedItem == null)
            {
                SelectedObject = null;
                return;
            }

            SelectedObject = (TeamType)lbObjectList.SelectedItem.Tag;
        }

        protected override void ListObjects()
        {
            lbObjectList.Clear();

            if (IncludeNone)
                lbObjectList.AddItem(Translate(this, "None", "None"));

            IEnumerable<TeamType> list = map.TeamTypes;
            if (chkIncludeGlobalTeamTypes.Checked)
                list = map.TeamTypes.UnionBy(map.Rules.TeamTypes, teamType => teamType.ININame);

            foreach (TeamType teamType in list)
            {
                lbObjectList.AddItem(new XNAListBoxItem() { Text = $"{teamType.ININame} {teamType.Name}", TextColor = teamType.GetXNAColor(), Tag = teamType });
                if (teamType == SelectedObject)
                    lbObjectList.SelectedIndex = lbObjectList.Items.Count - 1;
            }
        }
    }
}
