﻿using System;
using TSMapEditor.GameMath;
using TSMapEditor.Rendering;

namespace TSMapEditor.UI.CursorActions
{
    /// <summary>
    /// A cursor action that allows the user to select a cell.
    /// </summary>
    public class SelectCellCursorAction : CursorAction
    {
        public SelectCellCursorAction(ICursorActionTarget cursorActionTarget) : base(cursorActionTarget)
        {
        }

        public override bool DrawCellCursor => true;

        public event EventHandler<Point2D> CellSelected;

        public override void LeftClick(Point2D cellCoords)
        {
            CellSelected?.Invoke(this, cellCoords);
            ExitAction();
        }
    }
}
