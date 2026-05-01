using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using Rampastring.XNAUI.Input;
using System;
using System.Collections.Generic;
using TSMapEditor.GameMath;
using TSMapEditor.Models;
using TSMapEditor.Mutations.Classes;

namespace TSMapEditor.UI.CursorActions
{
    /// <summary>
    /// Cursor action for placing bridges.
    /// </summary>
    public class DrawConnectedTilesCursorAction : CursorAction
    {
        public DrawConnectedTilesCursorAction(ICursorActionTarget cursorActionTarget, ConnectedTileType connectedTileType) : base(cursorActionTarget)
        {
            this.connectedTileType = connectedTileType;
            ActionExited += UndoOnExit;
        }

        public override string GetName() => Translate("Name", "Draw Connected Tiles");

        public override bool HandlesKeyboardInput => true;

        public override bool DrawCellCursor => true;

        private readonly ConnectedTileType connectedTileType;

        private List<Point2D> connectedTilePath;
        private ConnectedTileSide connectedTileSide = ConnectedTileSide.Front;
        private DrawConnectedTilesMutation previewMutation;
        private byte extraHeight = 0;

        private int randomSeed = new Random().Next();

        public override void OnActionEnter()
        {
            connectedTilePath = new List<Point2D>();

            base.OnActionEnter();
        }

        public override void DrawPreview(Point2D cellCoords, Point2D cameraTopLeftPoint)
        {
            string mainText = Translate("MainText.V2", "Click on a cell to place a new vertex.\r\n\r\n" +
                "ENTER to confirm\r\n" +
                "Backspace to go back one step\r\n" +
                "R to re-generate the pattern\r\n");

            string tabText = Translate("TabText", "TAB to toggle between front and back sides\r\n");
            string pageUpDownText = Translate("PageUpDownText", "PageUp to raise the tiles, PageDown to lower them\r\n");
            string exitText = Translate("ExitText", "Right-click or ESC to exit");

            string text = (Constants.IsFlatWorld, connectedTileType.FrontOnly) switch
            {
                (true, true) => mainText + exitText,
                (true, false) => mainText + tabText + exitText,
                (false, true) => mainText + pageUpDownText + exitText,
                (false, false) => mainText + tabText + pageUpDownText + exitText
            };

            DrawText(cellCoords, cameraTopLeftPoint, 60, -150, text, Color.Yellow);

            Func<Point2D, Map, Point2D> getCellCenterPoint = Is2DMode ? CellMath.CellCenterPointFromCellCoords : CellMath.CellCenterPointFromCellCoords_3D;

            if (connectedTilePath.Count > 0)
            {
                Point2D start = connectedTilePath[0];
                start = getCellCenterPoint(start, CursorActionTarget.Map) - cameraTopLeftPoint;
                start = start.ScaleBy(CursorActionTarget.Camera.ZoomLevel);

                Color color = Color.Red;
                int precision = 8;
                int thickness = 3;
                Renderer.DrawCircle(start.ToXNAVector(), Constants.CellSizeY * 0.25f, color, precision, thickness);
            }

            // Draw cliff path
            for (int i = 0; i < connectedTilePath.Count - 1; i++)
            {
                Point2D start = connectedTilePath[i];
                start = getCellCenterPoint(start, CursorActionTarget.Map) - cameraTopLeftPoint;
                start = start.ScaleBy(CursorActionTarget.Camera.ZoomLevel);

                Point2D end = connectedTilePath[i + 1];
                end = getCellCenterPoint(end, CursorActionTarget.Map) - cameraTopLeftPoint;
                end = end.ScaleBy(CursorActionTarget.Camera.ZoomLevel);


                Color color = Color.Goldenrod;
                int thickness = 3;

                Renderer.DrawLine(start.ToXNAVector(), end.ToXNAVector(), color, thickness);
            }
        }

        public override void OnKeyPressed(KeyPressEventArgs e, Point2D cellCoords)
        {
            if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.Escape)
            {
                ExitAction();

                e.Handled = true;
            }
            else if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.Tab)
            {
                if (!connectedTileType.FrontOnly)
                {
                    connectedTileSide = connectedTileSide == ConnectedTileSide.Front ? ConnectedTileSide.Back : ConnectedTileSide.Front;
                    RedrawPreview();
                }

                e.Handled = true;
            }
            else if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.Back)
            {
                if (connectedTilePath.Count > 0)
                    connectedTilePath.RemoveAt(connectedTilePath.Count - 1);
                
                RedrawPreview();

                e.Handled = true;
            }
            else if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.R)
            {
                if (connectedTilePath.Count > 0)
                {
                    randomSeed = new Random().Next();
                    RedrawPreview();
                }

                e.Handled = true;
            }
            else if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.PageUp)
            {
                if (!Constants.IsFlatWorld)
                {
                    if (connectedTilePath.Count > 0)
                    {
                        if (MutationTarget.Map.GetTile(connectedTilePath[0]).Level + extraHeight + 1 <= Constants.MaxMapHeightLevel)
                            extraHeight++;
                    }

                    RedrawPreview();
                }

                e.Handled = true;
            }
            else if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.PageDown)
            {
                if (!Constants.IsFlatWorld)
                {
                    if (connectedTilePath.Count > 0)
                    {
                        if (MutationTarget.Map.GetTile(connectedTilePath[0]).Level + extraHeight - 1 >= 0)
                            extraHeight--;
                    }

                    RedrawPreview();
                }

                e.Handled = true;
            }
            else if (e.PressedKey == Microsoft.Xna.Framework.Input.Keys.Enter && connectedTilePath.Count >= 2)
            {
                previewMutation?.Undo();
                CursorActionTarget.MutationManager.PerformMutation(new DrawConnectedTilesMutation(MutationTarget, connectedTilePath, connectedTileType, connectedTileSide, randomSeed, extraHeight));

                ExitAction();

                e.Handled = true;
            }
        }

        public override void LeftClick(Point2D cellCoords)
        {
            connectedTilePath.Add(cellCoords);
            RedrawPreview();
        }

        private void RedrawPreview()
        {
            previewMutation?.Undo();

            if (connectedTilePath.Count >= 2)
            {
                previewMutation = new DrawConnectedTilesMutation(MutationTarget, connectedTilePath, connectedTileType, connectedTileSide, randomSeed, extraHeight);
                previewMutation.Perform();
            }
            else
            {
                previewMutation = null;
            }
        }

        private void UndoOnExit(object sender, EventArgs e)
        {
            previewMutation?.Undo();
        }
    }
}
