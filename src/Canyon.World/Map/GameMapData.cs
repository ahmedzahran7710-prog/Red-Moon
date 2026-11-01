using Canyon.Shared;
using Canyon.Shared.Mathematics;
using Microsoft.Extensions.Configuration.Ini;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Readers;
using System.Drawing;
using System.Text;

namespace Canyon.World.Map
{
    public class GameMapData
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<GameMapData>();

        private readonly uint idDoc;
        private readonly List<MapObject> mapObjects = new();
        private readonly List<PassageData> passageData = new();

        private readonly List<List<Layer>> setLayer = new();
        private Tile[,] cell;
        private Tile @default;

        public GameMapData(uint idMapDoc)
        {
            idDoc = idMapDoc;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public ref Tile this[int x, int y]
        {
            get
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                {
                    return ref @default;
                }

                return ref cell[x, y];
            }
        }

        public int GetPassage(int x, int y)
        {
            for (var cx = 0; cx < 9; cx++)
            {
                for (var cy = 0; cy < 9; cy++)
                {
                    int testX = x + WalkXCoords[cx];
                    int testY = y + WalkYCoords[cy];

                    for (var i = 0; i < passageData.Count; i++)
                    {
                        if (passageData[i].X == testX
                            && passageData[i].Y == testY)
                        {
                            return passageData[i].Index;
                        }
                    }
                }
            }

            return -1;
        }

        public int GetClosestPassage(int x, int y)
        {
            var passage = passageData.OrderBy(p => Calculations.GetDistance(x, y, p.X, p.Y)).FirstOrDefault();
            if (!passage.Equals(default) && Calculations.GetDistance(x, y, passage.X, passage.Y) <= 7)
            {
                return passage.Index;
            }
            return -1;
        }

        public bool Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                logger.LogWarning("Map data path is empty for map doc {IdDoc}.", idDoc);
                return false;
            }

            string realPath = GetRealPath(path);
            if (!File.Exists(realPath))
            {
                logger.LogWarning("Map data for file {IdDoc} '{Path}' has not been found.", idDoc, path);
                return false;
            }

            try
            {
                Stream stream = realPath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)
                    ? Read7zipFile(realPath)
                    : File.OpenRead(realPath);
                if (stream == null)
                {
                    logger.LogError("Could not open map archive '{Path}' for map doc {IdDoc}.", realPath, idDoc);
                    return false;
                }

                using (stream)
                using (BinaryReader reader = new(stream, Encoding.ASCII))
                {
                    LoadData(reader);
                    LoadPassageData(reader);
                    LoadLayerData(reader);
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load map data for map doc {IdDoc} from '{Path}'.", idDoc, realPath);
                return false;
            }
        }

        private void LoadData(BinaryReader reader)
        {
            reader.ReadUInt32(); // version
            reader.ReadUInt32(); // data
            reader.ReadBytes(260); // pul file
            Width = reader.ReadInt32();
            Height = reader.ReadInt32();

            cell = new Tile[Width, Height];
            for (var y = 0; y < Height; y++)
            {
                uint checkSum = 0;
                for (var x = 0; x < Width; x++)
                {
                    short access = reader.ReadInt16();
                    short surface = reader.ReadInt16();
                    short elevation = reader.ReadInt16();

                    checkSum += (uint)((uint)access * (surface + y + 1) +
                                        (elevation + 2) * (x + 1 + surface));

                    cell[x, y] = new Tile(elevation, access, surface);
                }

                uint tmp = reader.ReadUInt32();
                if (checkSum != tmp)
                {
                    logger.LogError("Invalid checksum for block of cells (mapdata: {IdDoc}), y: {Y}", idDoc, y);
                }
            }
        }

        private void LoadPassageData(BinaryReader reader)
        {
            int count = reader.ReadInt32();

            for (var i = 0; i < count; i++)
            {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                int index = reader.ReadInt32();
                passageData.Add(new PassageData(x, y, index));
            }
        }

        private void LoadLayerData(BinaryReader reader)
        {
            int count = reader.ReadInt32();

            for (var i = 0; i < count; i++)
            {
                int type = reader.ReadInt32();

                switch (type)
                {
                    case MAP_COVER:
                        reader.ReadChars(416);
                        break;

                    case MAP_TERRAIN:
                        var file = new string(reader.ReadChars(260));
                        int startX = reader.ReadInt32();
                        int startY = reader.ReadInt32();

                        int nullTerminator = file.IndexOf('\0');
                        if (nullTerminator >= 0)
                        {
                            file = file[..nullTerminator];
                        }
                        file = file.Replace("\\", Path.DirectorySeparatorChar.ToString());
                        if (File.Exists(file))
                        {
                            using BinaryReader scenery = new(File.OpenRead(file));
                            TerrainObject objTerrain = TerrainObject.CreateNew(scenery);
                            if (objTerrain != null)
                            {
                                objTerrain.SetPos(new Point(startX, startY));
                                AddMapObject(objTerrain);
                            }
                        }

                        break;

                    case MAP_SCENE:
                        break;

                    case MAP_SOUND:
                        reader.ReadChars(276);
                        break;

                    case MAP_3DEFFECT:
                        reader.ReadChars(72);
                        break;

                    case MAP_3DEFFECTNEW:
                        reader.ReadChars(276);
                        break;
                }
            }
        }

        public int GetFloorAttr(int x, int y)
        {
            Tile cell = this[x, y];
            if (cell.Equals(@default))
            {
                return 0;
            }

            return cell.GetFloorAttr(setLayer);
        }

        public int GetFloorAlt(int x, int y)
        {
            Tile cell = this[x, y];
            if (cell.Equals(@default))
            {
                return 0;
            }

            return cell.GetFloorAlt(setLayer);
        }

        public int GetFloorMask(int x, int y)
        {
            Tile cell = this[x, y];
            if (cell.Equals(@default))
            {
                return 0;
            }

            return cell.GetFloorMask(setLayer);
        }

        /// <summary>
        ///     This method has been created to avoid the case sensitive Linux path system, which would case us some trouble.
        /// </summary>
        private static string GetRealPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
            {
                return path;
            }

            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return path;
            }

            try
            {
                return Directory.EnumerateFiles(directory)
                    .FirstOrDefault(file => string.Equals(
                        Path.GetFileName(file),
                        fileName,
                        StringComparison.OrdinalIgnoreCase)) ?? path;
            }
            catch (IOException)
            {
                return path;
            }
            catch (UnauthorizedAccessException)
            {
                return path;
            }
        }

        public const int MAP_NONE = 0;
        public const int MAP_TERRAIN = 1;
        public const int MAP_TERRAIN_PART = 2;
        public const int MAP_SCENE = 3;
        public const int MAP_COVER = 4;
        public const int MAP_ROLE = 5;
        public const int MAP_HERO = 6;
        public const int MAP_PLAYER = 7;
        public const int MAP_PUZZLE = 8;
        public const int MAP_3DSIMPLE = 9;
        public const int MAP_3DEFFECT = 10;
        public const int MAP_2DITEM = 11;
        public const int MAP_3DNPC = 12;
        public const int MAP_3DOBJ = 13;
        public const int MAP_3DTRACE = 14;
        public const int MAP_SOUND = 15;
        public const int MAP_2DREGION = 16;
        public const int MAP_3DMAGICMAPITEM = 17;
        public const int MAP_3DITEM = 18;
        public const int MAP_3DEFFECTNEW = 19;
        public static readonly sbyte[] WalkXCoords = { 0, -1, -1, -1, 0, 1, 1, 1, 0 };
        public static readonly sbyte[] WalkYCoords = { 1, 1, 0, -1, -1, -1, 0, 1, 0 };

        public static readonly sbyte[] RideXCoords =
            {0, -2, -2, -2, 0, 2, 2, 2, -1, -2, -2, -1, 1, 2, 2, 1, -1, -2, -2, -1, 1, 2, 2, 1};

        public static readonly sbyte[] RideYCoords =
            {2, 2, 0, -2, -2, -2, 0, 2, 2, 1, -1, -2, -2, -1, 1, 2, 2, 1, -1, -2, -2, -1, 1, 2};

        #region Indexes

        public static int Pos2Index(int x, int y, int cx)
        {
            return x + y * cx;
        }

        public static int Index2X(int idx, int cy)
        {
            return idx % cy;
        }

        public static int Index2Y(int idx, int cy)
        {
            return idx / cy;
        }

        #endregion

        #region Map Object

        public bool AddMapObject(TerrainObject terrain)
        {
            if (terrain == null)
            {
                return false;
            }

            mapObjects.Add(terrain);
            return PlaceTerrainObject(terrain);
        }

        public bool DelMapObject(int idx)
        {
            if (idx < 0 || idx >= mapObjects.Count)
            {
                return false;
            }

            if (mapObjects[idx] is TerrainObject terrain && DisplaceTerrainObject(terrain))
            {
                mapObjects.RemoveAt(idx);
                return true;
            }

            return false;
        }

        public bool PlaceTerrainObject(TerrainObject terrain)
        {
            if (terrain == null)
            {
                return false;
            }

            try
            {
                //Console.WriteLine("================= Start =================");
                foreach (TerrainObjectPart part in terrain.Parts)
                {
                    for (var j = 0; j < part.SizeBaseCX; j++)
                    {
                        for (var k = 0; k < part.SizeBaseCY; k++)
                        {
                            Layer layer = part.GetLayer(j, k);
                            if (layer.Equals(default))
                            {
                                return false;
                            }

                            int x = terrain.X + part.PosSceneOffsetX + j - part.SizeBaseCX;
                            int y = terrain.Y + part.PosSceneOffsetY + k - part.SizeBaseCY;
                            if (x < 0 || y < 0)
                            {
                                continue;
                            }
                            if (x >= Width || y >= Height)
                            {
                                continue;
                            }
                            ref Tile tile = ref cell[x, y];
                            if (tile.Equals(default))
                            {
                                return false;
                            }
                            tile.AddLayer(setLayer, new Layer
                            {
                                Altitude = layer.Altitude,
                                Mask = layer.Mask,
                                Terrain = layer.Terrain
                            });
                        }
                    }
                }
                //Console.WriteLine("=================  End  =================");

                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to add terrain object on map doc {IdDoc} at {X},{Y}.", idDoc, terrain.X, terrain.Y);
                return false;
            }
        }

        public bool DisplaceTerrainObject(TerrainObject terrain)
        {
            if (terrain == null)
            {
                return false;
            }

            foreach (TerrainObjectPart part in terrain.Parts)
            {
                for (var j = 0; j < part.SizeBaseCX; j++)
                {
                    for (var k = 0; k < part.SizeBaseCY; k++)
                    {
                        int x = terrain.X + part.PosSceneOffsetX + j - part.SizeBaseCX;
                        int y = terrain.Y + part.PosSceneOffsetY + k - part.SizeBaseCY;
                        if (x < 0 || y < 0 || x >= Width || y >= Height)
                        {
                            continue;
                        }
                        ref Tile tile = ref cell[x, y];
                        if (tile.Equals(default))
                        {
                            return false;
                        }
                        tile.DelLayer(setLayer);
                    }
                }
            }

            return true;
        }

        #endregion

        #region Terrain Object

        public bool AddTerrainItem(uint idOwner, int x, int y, uint idTerrainType)
        {
            string iniDirectory = Path.Combine(Environment.CurrentDirectory, "ini");
            if (!Directory.Exists(iniDirectory))
            {
                logger.LogWarning("Cannot load terrain item {TerrainType}: ini directory '{Path}' is missing.", idTerrainType, iniDirectory);
                return false;
            }

            string terrainNpcFile = Directory.EnumerateFiles(iniDirectory)
                .FirstOrDefault(file => Path.GetFileName(file).Equals("terrainnpc.ini", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(terrainNpcFile))
            {
                logger.LogWarning("Cannot load terrain item {TerrainType}: terrainnpc.ini was not found.", idTerrainType);
                return false;
            }

            IniConfigurationSource source = new() { ReloadOnChange = false };
            IniConfigurationProvider reader = new(source);
            try
            {
                using FileStream iniStream = File.OpenRead(terrainNpcFile);
                reader.Load(iniStream);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read terrainnpc.ini from '{Path}'.", terrainNpcFile);
                return false;
            }

            string entry = $"NpcType{idTerrainType / 10}:Dir{idTerrainType % 10}";
            if (!reader.TryGet(entry, out string path) || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                logger.LogWarning("Terrain entry '{Entry}' points to a missing file '{Path}'.", entry, path);
                return false;
            }

            TerrainObject terrain;
            try
            {
                using BinaryReader scenery = new(File.OpenRead(path));
                terrain = TerrainObject.CreateNew(scenery, idOwner);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load terrain item {TerrainType} from '{Path}'.", idTerrainType, path);
                return false;
            }
            if (terrain == null)
            {
                return false;
            }

            terrain.SetPos(new Point(x, y));
            if (!AddMapObject(terrain))
            {
                return false;
            }
            return true;
        }

        public bool DelTerrainItem(uint idOwner)
        {
            for (var i = 0; i < mapObjects.Count; i++)
            {
                if (mapObjects[i] is TerrainObject terrain)
                {
                    if (terrain.OwnerIdentity == idOwner)
                    {
                        return DelMapObject(i);
                    }
                }
            }

            return true;
        }

        #endregion

        #region Unzip Data

        private Stream Read7zipFile(string path)
        {
            if (ArchiveFactory.IsArchive(path, out _))
            {
                using MemoryStream stream = new MemoryStream();
                using IArchive archive = ArchiveFactory.Open(path);
                using IReader reader = archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        using var entry = reader.OpenEntryStream();
                        entry.CopyTo(stream);
                    }
                }
                return new MemoryStream(stream.ToArray());
            }
            return null;
        }
        #endregion
    }

    public struct PassageData
    {
        public PassageData(int x, int y, int index)
        {
            X = x;
            Y = y;
            Index = index;
        }

        public int X;
        public int Y;
        public int Index;
    }
}
