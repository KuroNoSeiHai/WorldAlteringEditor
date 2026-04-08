using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using System;
using System.Collections.Generic;
using TSMapEditor.GameMath;
using TSMapEditor.Models;
using TSMapEditor.Mutations.Classes;
using TSMapEditor.Rendering;

namespace TSMapEditor.UI.CursorActions
{
    public class ConnectedOverlayPlacementAction : CursorAction
    {
        public ConnectedOverlayPlacementAction(ICursorActionTarget cursorActionTarget) : base(cursorActionTarget)
        {
        }

        public override string GetName() => Translate("Name", "Place Connected Overlay");
        public ConnectedOverlayType ConnectedOverlayType { get; set; }
        struct OriginalOverlayInfo
        {
            public OverlayType OverlayType;
            public int FrameIndex;

            public OriginalOverlayInfo(OverlayType overlayType, int frameIndex)
            {
                OverlayType = overlayType;
                FrameIndex = frameIndex;
            }
        }

        private List<OriginalOverlayInfo> originalOverlay = new List<OriginalOverlayInfo>();

        private Point2D? lineSourceCell;
        private PlaceConnectedOverlayLineMutation linePreviewMutation;
        private bool blocked;

        public override void OnActionExit()
        {
            ClearLinePreview();
            base.OnActionExit();
        }

        private (Direction direction, int length) GetLineInformation(Point2D cellCoords)
        {
            Direction direction = Helpers.DirectionFromPoints(lineSourceCell.Value, cellCoords);
            Point2D vector = cellCoords - lineSourceCell.Value;
            int length = Math.Max(Math.Abs(vector.X), Math.Abs(vector.Y));

            return (direction, length);
        }

        public override void PreMapDraw(Point2D cellCoords)
        {
            if (lineSourceCell.HasValue)
            {
                ApplyLinePreview(cellCoords);
                return;
            }

            originalOverlay.Clear();

            CursorActionTarget.BrushSize.DoForBrushSizeAndSurroundings(offset =>
            {
                var tile = CursorActionTarget.Map.GetTile(cellCoords + offset);
                if (tile == null)
                    return;

                // Store original overlay info
                originalOverlay.Add(tile.Overlay != null
                    ? new OriginalOverlayInfo(tile.Overlay.OverlayType, tile.Overlay.FrameIndex)
                    : new OriginalOverlayInfo(null, Constants.NO_OVERLAY));
            });

            new PlaceConnectedOverlayMutation(CursorActionTarget.MutationTarget, ConnectedOverlayType, cellCoords).Perform();
        }

        public override void PostMapDraw(Point2D cellCoords)
        {
            if (lineSourceCell.HasValue)
            {
                ClearLinePreview();
                return;
            }

            int index = 0;

            CursorActionTarget.BrushSize.DoForBrushSizeAndSurroundings(offset =>
            {
                var tile = CursorActionTarget.Map.GetTile(cellCoords + offset);
                if (tile == null)
                    return;

                var originalOverlayData = originalOverlay[index];

                if (originalOverlayData.OverlayType == null)
                {
                    tile.Overlay = null;
                }
                else
                {
                    tile.Overlay.OverlayType = originalOverlayData.OverlayType;
                    tile.Overlay.FrameIndex = originalOverlayData.FrameIndex;
                }

                index++;
            });

            originalOverlay.Clear();

            CursorActionTarget.AddRefreshPoint(cellCoords, Math.Max(CursorActionTarget.BrushSize.Height, CursorActionTarget.BrushSize.Width));
        }

        private void ApplyLinePreview(Point2D cellCoords)
        {
            if (!lineSourceCell.HasValue || lineSourceCell.Value == cellCoords)
                return;

            (Direction direction, int length) = GetLineInformation(cellCoords);

            if (length < 1)
                return;

            linePreviewMutation = new PlaceConnectedOverlayLineMutation(CursorActionTarget.MutationTarget, ConnectedOverlayType, lineSourceCell.Value, direction, length);
            linePreviewMutation.Perform();
        }

        private void ClearLinePreview()
        {
            if (linePreviewMutation != null)
            {
                linePreviewMutation.Undo();
                linePreviewMutation = null;
            }

            CursorActionTarget.InvalidateMap();
        }

        public override void DrawPreview(Point2D cellCoords, Point2D cameraTopLeftPoint)
        {
            if (!lineSourceCell.HasValue)
                return;

            if (cellCoords == lineSourceCell.Value)
                return;

            (Direction direction, int length) = GetLineInformation(cellCoords);

            Point2D cameraPoint1 = (CellMath.CellCenterPointFromCellCoords_3D(lineSourceCell.Value, Map) - cameraTopLeftPoint).ScaleBy(CursorActionTarget.Camera.ZoomLevel);
            Point2D cameraPoint2 = (CellMath.CellCenterPointFromCellCoords_3D(lineSourceCell.Value + Helpers.VisualDirectionToPoint(direction).ScaleBy(length), Map) - cameraTopLeftPoint).ScaleBy(CursorActionTarget.Camera.ZoomLevel);

            Renderer.DrawLine(cameraPoint1.ToXNAVector(), cameraPoint2.ToXNAVector(), Color.Orange, 2);
        }

        public override void LeftDown(Point2D cellCoords)
        {
            if (blocked)
                return;

            if (KeyboardCommands.Instance.PlaceTerrainLine.AreKeysOrModifiersDown(Keyboard))
            {
                if (lineSourceCell == null && CursorActionTarget.Map.GetTile(cellCoords) != null)
                {
                    lineSourceCell = cellCoords;
                }

                return;
            }

            var mutation = new PlaceConnectedOverlayMutation(CursorActionTarget.MutationTarget, ConnectedOverlayType, cellCoords);
            CursorActionTarget.MutationManager.PerformMutation(mutation);
        }

        private void ApplyLine(Point2D cellCoords)
        {
            (Direction direction, int length) = GetLineInformation(cellCoords);
            var mutation = new PlaceConnectedOverlayLineMutation(CursorActionTarget.MutationTarget, ConnectedOverlayType, lineSourceCell.Value, direction, length);
            PerformMutation(mutation);
            lineSourceCell = null;
        }

        public override void LeftClick(Point2D cellCoords)
        {
            if (KeyboardCommands.Instance.PlaceTerrainLine.AreKeysOrModifiersDown(Keyboard))
            {
                if (lineSourceCell != null && cellCoords != lineSourceCell.Value)
                {
                    ApplyLine(cellCoords);
                }

                return;
            }

            LeftDown(cellCoords);
            blocked = false;
        }

        public override void Update(Point2D? cellCoords)
        {
            if (lineSourceCell != null && cellCoords != null && lineSourceCell != cellCoords)
            {
                if (!KeyboardCommands.Instance.PlaceTerrainLine.AreKeysOrModifiersDown(Keyboard))
                {
                    ApplyLine(cellCoords.Value);
                    blocked = true;
                }
            }

            if (!CursorActionTarget.WindowManager.Cursor.LeftDown && !CursorActionTarget.WindowManager.Cursor.LeftClicked)
                blocked = false;
        }
    }
}
