using Microsoft.Xna.Framework;
using Rampastring.XNAUI;
using System;
using TSMapEditor.GameMath;
using TSMapEditor.Models;
using TSMapEditor.Mutations.Classes;

namespace TSMapEditor.UI.CursorActions
{
    /// <summary>
    /// A cursor action that allows placing down terrain objects.
    /// </summary>
    public class TerrainObjectPlacementAction : CursorAction
    {
        public TerrainObjectPlacementAction(ICursorActionTarget cursorActionTarget) : base(cursorActionTarget)
        {
        }

        public override string GetName() => Translate("Name", "Place Terrain Object");

        private TerrainObject terrainObject;

        private TerrainType _terrainType;

        public TerrainType TerrainType
        {
            get => _terrainType;
            set
            {
                if (_terrainType != value)
                {
                    _terrainType = value;

                    if (_terrainType == null)
                    {
                        terrainObject = null;
                    }
                    else
                    {
                        terrainObject = new TerrainObject(_terrainType);
                    }
                }
            }
        }

        private Point2D? lineSourceCell;
        private PlaceTerrainObjectLineMutation linePreviewMutation;
        private bool blocked;

        public override void InactiveUpdate()
        {
            lineSourceCell = null;
            blocked = false;

            if (linePreviewMutation != null)
                ClearLinePreview();
        }

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

            var cell = CursorActionTarget.Map.GetTile(cellCoords);
            if (cell.TerrainObject == null)
            {
                terrainObject.Position = cell.CoordsToPoint();
                cell.TerrainObject = terrainObject;
            }

            CursorActionTarget.AddRefreshPoint(cellCoords);
        }

        public override void PostMapDraw(Point2D cellCoords)
        {
            if (lineSourceCell.HasValue)
            {
                ClearLinePreview();
                return;
            }

            var cell = CursorActionTarget.Map.GetTile(cellCoords);
            if (cell.TerrainObject == terrainObject)
            {
                cell.TerrainObject = null;
            }

            CursorActionTarget.AddRefreshPoint(cellCoords);
        }

        private void ApplyLinePreview(Point2D cellCoords)
        {
            if (!lineSourceCell.HasValue || lineSourceCell.Value == cellCoords)
                return;

            (Direction direction, int length) = GetLineInformation(cellCoords);

            if (length < 1)
                return;

            linePreviewMutation = new PlaceTerrainObjectLineMutation(CursorActionTarget.MutationTarget, TerrainType, lineSourceCell.Value, direction, length);
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
            if (_terrainType == null)
                throw new InvalidOperationException(nameof(TerrainType) + " cannot be null");

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

            var cell = CursorActionTarget.Map.GetTile(cellCoords);
            if (cell.TerrainObject != null)
                return;

            var mutation = new PlaceTerrainObjectMutation(CursorActionTarget.MutationTarget, TerrainType, cellCoords);
            CursorActionTarget.MutationManager.PerformMutation(mutation);
        }

        private void ApplyLine(Point2D cellCoords)
        {
            (Direction direction, int length) = GetLineInformation(cellCoords);
            var mutation = new PlaceTerrainObjectLineMutation(CursorActionTarget.MutationTarget, TerrainType, lineSourceCell.Value, direction, length);
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
