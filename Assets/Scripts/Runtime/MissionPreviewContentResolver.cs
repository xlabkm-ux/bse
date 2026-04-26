using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BreachScenarioEngine.Runtime
{
    public sealed class MissionPreviewContentSet
    {
        public string Mode { get; private set; } = "debug_fallback";
        public int DebugFallbackCount { get; private set; }
        public TileBase FloorTile { get; private set; }
        public TileBase WallTile { get; private set; }
        public Sprite MarkerSprite { get; private set; }
        public Color DoorColor { get; private set; }
        public Color WindowColor { get; private set; }
        public Color BreachColor { get; private set; }
        public Color CoverColor { get; private set; }
        public Color OperativeColor { get; private set; }
        public Color HostageColor { get; private set; }
        public Color EnemyColor { get; private set; }
        public Color ObjectiveColor { get; private set; }
        public Color ExtractionColor { get; private set; }

        internal static MissionPreviewContentSet CreateDebugFallback()
        {
            var sprite = MissionPreviewContentResolver.SharedDebugSprite();
            return new MissionPreviewContentSet
            {
                Mode = "debug_fallback",
                DebugFallbackCount = 11,
                FloorTile = MissionPreviewContentResolver.DebugTile("floor", new Color(0.18f, 0.22f, 0.24f, 0.72f)),
                WallTile = MissionPreviewContentResolver.DebugTile("wall", new Color(0.78f, 0.82f, 0.84f, 1f)),
                MarkerSprite = sprite,
                DoorColor = new Color(0.9f, 0.72f, 0.22f, 1f),
                WindowColor = new Color(0.7f, 0.85f, 0.98f, 1f),
                BreachColor = new Color(0.95f, 0.42f, 0.24f, 1f),
                CoverColor = new Color(0.28f, 0.55f, 0.42f, 1f),
                OperativeColor = new Color(0.24f, 0.62f, 0.9f, 1f),
                HostageColor = new Color(0.92f, 0.82f, 0.34f, 1f),
                EnemyColor = new Color(0.84f, 0.25f, 0.22f, 1f),
                ObjectiveColor = new Color(0.56f, 0.36f, 0.86f, 1f),
                ExtractionColor = new Color(0.42f, 0.9f, 0.45f, 0.4f)
            };
        }
    }

    public static class MissionPreviewContentResolver
    {
        private static readonly Dictionary<string, TileBase> TileCache = new(StringComparer.Ordinal);
        private static Sprite sharedSprite;

        public static MissionPreviewContentSet Resolve(MissionConfig config)
        {
            return MissionPreviewContentSet.CreateDebugFallback();
        }

        internal static TileBase DebugTile(string key, Color color)
        {
            if (TileCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = SharedDebugSprite();
            tile.color = color;
            tile.name = key + "_debug_preview_tile";
            tile.hideFlags = HideFlags.HideAndDontSave;
            TileCache[key] = tile;
            return tile;
        }

        internal static Sprite SharedDebugSprite()
        {
            if (sharedSprite != null)
            {
                return sharedSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "BSE_Debug_Preview_Sprite";
            sharedSprite.hideFlags = HideFlags.HideAndDontSave;
            return sharedSprite;
        }
    }
}
