using System;
using System.Collections.Generic;
using UnityEngine;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Death markers: dashed hollow circles drawn at each ball's position at the
    /// moment the player dies. Markers persist per-chart across play sessions —
    /// hidden when leaving play mode, re-shown when play starts again, dropped
    /// when a different chart is loaded, and removed once the player passes the
    /// floor where the death happened.
    /// </summary>
    public static class DeathMarker
    {
        private sealed class MarkerData
        {
            public Vector3 Position;
            public int SeqID;
            public GameObject? Object;
        }

        private static readonly List<MarkerData> _markers = new();
        private static string? _chartPath;

        public static void Mark(IEnumerable<Vector3> positions, int seqID)
        {
            if (!Main.Settings.Enabled || !Main.Settings.ShowDeathMarkers) return;

            DestroyMarkers();
            foreach (var pos in positions)
            {
                _markers.Add(new MarkerData { Position = pos, SeqID = seqID });
            }

            // Remember which chart these markers belong to so re-entering play for
            // a different chart doesn't resurrect stale markers.
            try { _chartPath = ADOBase.levelPath; } catch { _chartPath = null; }

            Show();
        }

        /// <summary>Create (or re-create) marker objects from the stored positions.</summary>
        public static void Show()
        {
            if (!Main.Settings.Enabled || !Main.Settings.ShowDeathMarkers)
            {
                Hide();
                return;
            }

            // If a different chart is now loaded, the stored markers are stale.
            string? current = null;
            try { current = ADOBase.levelPath; } catch { current = null; }
            if (!string.Equals(_chartPath, current, StringComparison.Ordinal))
            {
                Clear();
                return;
            }

            if (_markers.Count == 0)
            {
                Hide();
                return;
            }

            // Rebuild only if the markers were destroyed (scene change) or are hidden.
            if (_markers[0].Object != null && !_markers[0].Object!.activeSelf)
            {
                foreach (var m in _markers)
                    if (m.Object != null) m.Object.SetActive(true);
                return;
            }

            foreach (var m in _markers)
            {
                if (m.Object == null)
                    m.Object = CreateMarker(m.Position);
                else if (!m.Object.activeSelf)
                    m.Object.SetActive(true);
            }
        }

        /// <summary>
        /// Remove markers whose floor has been passed (called every frame while
        /// playing).
        /// </summary>
        public static void OnUpdate()
        {
            if (_markers.Count == 0) return;

            var ctl = scrController.instance;
            if (ctl == null) return;
            if (ctl.currentSeqID <= 0) return;

            for (var i = _markers.Count - 1; i >= 0; i--)
            {
                if (_markers[i].SeqID >= ctl.currentSeqID) continue;
                if (_markers[i].Object != null)
                    UnityEngine.Object.Destroy(_markers[i].Object);
                _markers.RemoveAt(i);
            }
        }

        /// <summary>Hide the markers but keep the stored positions.</summary>
        public static void Hide()
        {
            foreach (var m in _markers)
                if (m.Object != null) m.Object.SetActive(false);
        }

        /// <summary>Destroy markers and forget their positions.</summary>
        public static void Clear()
        {
            DestroyMarkers();
            _markers.Clear();
            _chartPath = null;
        }

        private static void DestroyMarkers()
        {
            foreach (var m in _markers)
                if (m.Object != null) UnityEngine.Object.Destroy(m.Object);
            _markers.Clear();
        }

        private static GameObject CreateMarker(Vector3 center)
        {
            var go = new GameObject("JEA DeathMarker");
            go.transform.position = center;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = new Material(Shader.Find("ADOFAI/ScrollingSprite"));
            lr.material.SetTexture("_MainTex", ADOBase.gc?.planetPolygonTex);
            lr.material.SetVector("_ScrollSpeed", Vector2.zero);
            lr.material.SetFloat("_Time0", 0f);
            lr.textureMode = LineTextureMode.Tile;
            lr.startWidth = 0.06f;
            lr.endWidth = 0.06f;
            lr.startColor = new Color(1f, 1f, 1f, 0.7f);
            lr.endColor = new Color(1f, 1f, 1f, 0.7f);
            lr.sortingLayerName = "Floor";
            lr.sortingOrder = 1000;

            var ctl = scrController.instance;
            float radius = ctl != null ? ctl.tileSize * 0.25f : 0.5f;
            const int segments = 48;
            lr.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var a = (float)i / segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }

            return go;
        }
    }
}