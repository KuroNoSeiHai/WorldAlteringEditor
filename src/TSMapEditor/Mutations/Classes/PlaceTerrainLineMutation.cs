using System;
using System.Collections.Generic;
using TSMapEditor.GameMath;
using TSMapEditor.Models;
using TSMapEditor.Rendering;
using TSMapEditor.UI;

namespace TSMapEditor.Mutations.Classes
{
    public class PlaceTerrainLineMutation : Mutation
    {
        public PlaceTerrainLineMutation(IMutationTarget mutationTarget, MapTile source, Direction direction, int length, TileImage tile) : base(mutationTarget)
        {
            sourceTile = source;
            this.direction = direction;
            this.length = length;
            this.tile = tile;
        }

        private readonly MapTile sourceTile;
        private readonly Direction direction;
        private readonly int length;
        private readonly TileImage tile;
        private List<OriginalCellTerrainData> undoData = new List<OriginalCellTerrainData>();

        public override string GetDisplayString()
        {
            string directionString = Translate("Direction." + direction, Helpers.DirectionToName(direction));
            return string.Format(Translate(this, "DisplayString", "Place Terrain Line from {0} towards {1} with distance of {2}"), sourceTile.CoordsToPoint(), direction, length);
        }

        int GetSouthernmostSubTileOffset()
        {
            int maxY = 1;

            for (int i = 0; i < tile.TMPImages.Length; i++)
            {
                var image = tile.TMPImages[i];

                if (image == null)
                    continue;

                int cx = i % tile.Width;
                int cy = i / tile.Height;

                int southOffset = (cx + cy) / 2;
                if (southOffset > maxY)
                    maxY = southOffset;
            }

            return maxY;
        }

        int GetEasternmostSubTileOffset()
        {
            int maxEast = 1;

            for (int i = 0; i < tile.TMPImages.Length; i++)
            {
                var image = tile.TMPImages[i];

                if (image == null)
                    continue;

                int cx = i % tile.Width;
                int cy = i / tile.Height;

                int eastOffset = cx - cy;

                if (eastOffset > maxEast)
                    maxEast = eastOffset;
            }

            return maxEast;
        }

        int GetWesternmostSubTileOffset()
        {
            int maxWest = 1;

            for (int i = 0; i < tile.TMPImages.Length; i++)
            {
                var image = tile.TMPImages[i];

                if (image == null)
                    continue;

                int cx = i % tile.Width;
                int cy = i / tile.Height;

                int westOffset = cy - cx;

                if (westOffset > maxWest)
                    maxWest = westOffset;
            }

            return maxWest;
        }

        public override void Perform()
        {
            int processedLength = 0;
            while (processedLength <= length)
            {
                Point2D coords = sourceTile.CoordsToPoint() + Helpers.VisualDirectionToPoint(direction).ScaleBy(processedLength);

                PlaceTile(coords);

                switch (direction)
                {
                    case Direction.NE:
                    case Direction.SW:
                        processedLength += tile.Height;
                        break;
                    case Direction.NW:
                    case Direction.SE:
                        processedLength += tile.Width;
                        break;
                    case Direction.N:
                    case Direction.S:
                        processedLength += GetSouthernmostSubTileOffset();
                        break;
                    case Direction.E:
                        processedLength += GetEasternmostSubTileOffset();
                        break;
                    case Direction.W:
                        processedLength += GetWesternmostSubTileOffset();
                        break;
                }
            }

            MutationTarget.InvalidateMap();
        }

        private void PlaceTile(Point2D coords)
        {
            var originalData = new List<OriginalCellTerrainData>();

            // Process cells within tile foundation
            for (int i = 0; i < tile.TMPImages.Length; i++)
            {
                MGTMPImage image = tile.TMPImages[i];

                if (image == null)
                    continue;

                int cx = coords.X + i % tile.Width;
                int cy = coords.Y + i / tile.Width;

                Point2D cellCoords = new Point2D(cx, cy);
                var cell = Map.GetTile(cellCoords);
                if (cell == null)
                    continue;

                undoData.Add(new OriginalCellTerrainData(cellCoords, cell.TileIndex, cell.SubTileIndex, cell.Level));
                cell.ChangeTileIndex(tile.TileID, (byte)i);
            }
        }

        public override void Undo()
        {
            for (int i = 0; i < undoData.Count; i++)
            {
                OriginalCellTerrainData originalTerrainData = undoData[i];

                var mapCell = MutationTarget.Map.GetTile(originalTerrainData.CellCoords);
                if (mapCell != null)
                {
                    mapCell.ChangeTileIndex(originalTerrainData.TileIndex, originalTerrainData.SubTileIndex);
                    mapCell.Level = originalTerrainData.HeightLevel;
                    RefreshCellLighting(mapCell);
                }
            }

            MutationTarget.InvalidateMap();
        }
    }
}
