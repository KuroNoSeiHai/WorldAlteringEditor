using Rampastring.XNAUI.Input;
using System;
using System.Collections.Generic;
using TSMapEditor.GameMath;
using TSMapEditor.Models;
using TSMapEditor.Mutations;
using TSMapEditor.Mutations.Classes;
using TSMapEditor.Rendering;

namespace TSMapEditor.UI.CursorActions
{
    public class PlaceTerrainCursorAction : LineAndRegularPaintingAction
    {
        public PlaceTerrainCursorAction(ICursorActionTarget cursorActionTarget) : base(cursorActionTarget)
        {
        }

        public override string GetName() => Translate("Name", "Place Terrain Tiles");

        public override bool HandlesKeyboardInput => true;

        private TileImage _tile;
        public TileImage Tile
        {
            get => _tile;
            set
            {
                _tile = value;
                heightOffset = 0;
            }
        }

        private int heightOffset;

        private HashSet<MapTile> previewTiles = new HashSet<MapTile>();

        public override void OnActionEnter()
        {
            heightOffset = 0;
            base.OnActionEnter();
        }

        public override void OnActionExit()
        {
            ClearPreview();
            base.OnActionExit();
        }

        public override void OnKeyPressed(KeyPressEventArgs e, Point2D cellCoords)
        {
            if (Constants.IsFlatWorld)
                return;

            if (KeyboardCommands.Instance.AdjustTileHeightDown.Key.Key == e.PressedKey)
            {
                if (heightOffset > -Constants.MaxMapHeight)
                    heightOffset--;

                e.Handled = true;
            }
            else if (KeyboardCommands.Instance.AdjustTileHeightUp.Key.Key == e.PressedKey)
            {
                if (heightOffset < Constants.MaxMapHeight)
                    heightOffset++;

                e.Handled = true;
            }
        }

        private Point2D GetAdjustedCellCoords(Point2D cellCoords)
        {
            if (KeyboardCommands.Instance.PlaceTerrainBelow.AreKeysOrModifiersDown(CursorActionTarget.WindowManager.Keyboard))
                return cellCoords;

            // Don't place the tile where the user is pointing the cursor to,
            // but slightly above it - FinalSun also does this to not obstruct
            // the user's map view with the cursor
            int height = Tile.GetHeight();
            int cellHeight = (height / Constants.CellSizeY) - 1;

            Point2D newCellCoords = cellCoords - new Point2D(cellHeight, cellHeight);

            return newCellCoords;
        }

        public override void PreMapDraw(Point2D cellCoords)
        {
            // Assign preview data
            ApplyPreviewForCells(cellCoords);
        }

        public override void PostMapDraw(Point2D cellCoords)
        {
            ClearPreview();
        }

        private void ClearPreview()
        {
            // Clear preview data
            foreach (var cell in previewTiles)
            {
                cell.PreviewTileImage = null;
                cell.PreviewLevel = -1;
            }

            if (LinePreviewMutation != null)
            {
                LinePreviewMutation.Undo();
                LinePreviewMutation = null;
            }

            previewTiles.Clear();
            CursorActionTarget.InvalidateMap();
        }

        private void ApplyTerrainLinePreview(Point2D cellCoords)
        {
            Point2D adjustedCellCoords = GetAdjustedCellCoords(cellCoords);

            MapTile originTile = CursorActionTarget.Map.GetTile(cellCoords);

            Direction direction;
            int length;

            if (LineSourceCell.Value == adjustedCellCoords)
            {
                direction = default;
                length = 1;
            }
            else
            {
                (direction, length) = GetLineInformation(adjustedCellCoords);
            }

            if (length < 2)
            {
                Tile.DoForValidSubTiles((subTile, subTileOffset, subTileIndex) =>
                {
                    var mapTile = Map.GetTile(adjustedCellCoords + subTileOffset);

                    if (mapTile != null && (!CursorActionTarget.OnlyPaintOnClearGround || mapTile.IsClearGround()))
                    {
                        mapTile.PreviewSubTileIndex = subTileIndex;
                        mapTile.PreviewLevel = Math.Min(mapTile.Level + subTile.TmpImage.Height, Constants.MaxMapHeightLevel);
                        mapTile.PreviewTileImage = Tile;
                        previewTiles.Add(mapTile);
                    }
                });
            }
            else
            {
                LinePreviewMutation = new PlaceTerrainLineMutation(MutationTarget, Map.GetTile(LineSourceCell.Value), direction, length, Tile);
                LinePreviewMutation.Perform();
            }
        }

        private void ApplyPreviewForCells(Point2D cellCoords)
        {
            if (Tile == null)
                return;

            if (LineSourceCell.HasValue)
            {
                ApplyTerrainLinePreview(cellCoords);
                return;
            }

            Point2D adjustedCellCoords = GetAdjustedCellCoords(cellCoords);

            MapTile originTile = CursorActionTarget.Map.GetTile(adjustedCellCoords);
            int originLevel = -1;

            BrushSize brush = CursorActionTarget.BrushSize;

            // First, look up the lowest point within the tile area for origin level
            // Only use a 1x1 brush size for this (meaning no brush at all)
            // so users can use larger brush sizes to "paint height"

            for (int i = 0; i < Tile.TMPImages.Length; i++)
            {
                Point2D? subTileOffset = Tile.GetSubTileCoordOffset(i);

                if (subTileOffset == null)
                    continue;

                var mapTile = MutationTarget.Map.GetTile(adjustedCellCoords + subTileOffset.Value);

                if (mapTile != null)
                {
                    var existingTile = Map.TheaterInstance.GetTile(mapTile.TileIndex).GetSubTile(mapTile.SubTileIndex);

                    int cellLevel = mapTile.Level;

                    // Allow replacing back cliffs
                    if (existingTile.TmpImage.Height == Tile.GetSubTile(i).TmpImage.Height)
                        cellLevel -= existingTile.TmpImage.Height;

                    if (originLevel < 0 || cellLevel < originLevel)
                        originLevel = cellLevel;
                }
            }

            originLevel += heightOffset;
            if (originLevel < 0)
                originLevel = 0;

            // Then apply the preview data
            brush.DoForBrushSize(offset =>
            {
                for (int i = 0; i < Tile.TMPImages.Length; i++)
                {
                    Point2D? subTileOffset = Tile.GetSubTileCoordOffset(i);

                    if (subTileOffset == null)
                        continue;

                    var mapTile = Map.GetTile(adjustedCellCoords + new Point2D(offset.X * Tile.Width, offset.Y * Tile.Height) + subTileOffset.Value);

                    if (mapTile != null && (!CursorActionTarget.OnlyPaintOnClearGround || mapTile.IsClearGround()))
                    {
                        mapTile.PreviewSubTileIndex = i;
                        mapTile.PreviewLevel = Math.Min(originLevel + Tile.GetSubTile(i).TmpImage.Height, Constants.MaxMapHeightLevel);
                        mapTile.PreviewTileImage = Tile;
                        previewTiles.Add(mapTile);
                    }
                }
            });

            if (CursorActionTarget.AutoLATEnabled)
            {
                // Get potential base tilesets of the placed LAT (if we're placing LAT)
                // This allows placing certain LATs on top of other LATs (example: snowy dirt on snow, when snow is also placed on grass)
                (var baseTileSet, var altBaseTileSet) = Mutation.GetBaseTileSetsForTileSet(Map.TheaterInstance, Tile.TileSetId);

                // Calculate total area to apply Auto-LAT into
                int totalWidth = (Tile.Width * brush.Width) + 1;
                int totalHeight = (Tile.Height * brush.Height) + 1;

                for (int y = -1; y < totalHeight; y++)
                {
                    for (int x = -1; x < totalWidth; x++)
                    {
                        int cx = adjustedCellCoords.X + x;
                        int cy = adjustedCellCoords.Y + y;

                        var cell = Map.GetTile(cx, cy);
                        if (cell == null)
                            continue;

                        int autoLatTileIndex = Mutation.GetAutoLATTileIndexForCell(Map, cell.CoordsToPoint(), baseTileSet, altBaseTileSet, true);

                        if (autoLatTileIndex > -1)
                        {
                            cell.PreviewTileImage = CursorActionTarget.TheaterGraphics.GetTileGraphics(autoLatTileIndex, 0);
                            cell.PreviewSubTileIndex = 0;
                            if (cell.PreviewLevel < 0)
                                cell.PreviewLevel = cell.Level;
                            previewTiles.Add(cell);
                        }
                    }
                }
            }

            CursorActionTarget.AddRefreshPoint(adjustedCellCoords, Math.Max(Tile.Width, Tile.Height) * Math.Max(brush.Width, brush.Height) + 1);
        }

        protected override bool CanDrawLinePreview() => Tile != null;

        protected override ICheckableMutation CreateRegularPlacementMutation(Point2D cellCoords)
        {
            return new PlaceTerrainTileMutation(CursorActionTarget.MutationTarget, cellCoords, Tile, heightOffset);
        }

        protected override Mutation CreateLinePlacementMutation(Direction direction, int length)
        {
            return new PlaceTerrainLineMutation(MutationTarget, Map.GetTile(LineSourceCell.Value), direction, length, Tile);
        }

        protected override void ApplyLine(Point2D cellCoords)
        {
            var adjustedCellCoords = GetAdjustedCellCoords(cellCoords);
            (Direction direction, int length) = GetLineInformation(adjustedCellCoords);
            var mutation = CreateLinePlacementMutation(direction, length);
            PerformMutation(mutation);
        }

        public override void LeftDown(Point2D cellCoords)
        {
            if (Tile == null)
                return;

            if (Blocked)
                return;

            Point2D adjustedCellCoords = GetAdjustedCellCoords(cellCoords);

            if (KeyboardCommands.Instance.PlaceTerrainLine.AreKeysOrModifiersDown(Keyboard))
            {
                var targetCell = CursorActionTarget.Map.GetTile(adjustedCellCoords);

                if (LineSourceCell == null && targetCell != null)
                {
                    LineSourceCell = adjustedCellCoords;
                    PreviousCellCoords = adjustedCellCoords;
                }

                return;
            }

            if (PreviousCellCoords != cellCoords)
            {
                if (KeyboardCommands.Instance.FillTerrain.AreKeysOrModifiersDown(Keyboard)
                    && (Tile.Width == 1 && Tile.Height == 1))
                {
                    var targetCell = CursorActionTarget.Map.GetTile(adjustedCellCoords);

                    if (targetCell != null)
                    {
                        var mutation = new FillTerrainAreaMutation(CursorActionTarget.MutationTarget, targetCell, Tile);
                        CursorActionTarget.MutationManager.PerformMutation(mutation);
                        PreviousCellCoords = cellCoords;
                    }

                    return;
                }

                var tileMutation = new PlaceTerrainTileMutation(CursorActionTarget.MutationTarget, adjustedCellCoords, Tile, heightOffset);
                CursorActionTarget.MutationManager.PerformMutation(tileMutation);
                PreviousCellCoords = cellCoords;
            }
        }
    }
}
