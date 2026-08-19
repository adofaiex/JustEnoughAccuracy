using System;
using System.Collections.Generic;
using UnityEngine;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Hit-position markers: gold dashed circles drawn at every ball's position
    /// on the tile the player selected in the previewer. Supports up to 8 balls
    /// (matching the game's PlanetarySystem limit). Circles rotate slowly for a
    /// visual effect. When the player clicks a different hit the previous markers
    /// are destroyed.
    /// </summary>
    public static class HitMarker
    {
        private static readonly List<GameObject> _objects = new();
        private static float _angle;

        /// <summary>Show markers at every ball position for a given floor.</summary>
        public static void Show(scrFloor floor)
        {
            Clear();
            if (floor == null) return;
            var positions = CalculateBallPositions(floor);
            foreach (var pos in positions)
                _objects.Add(CreateMarker(pos));
        }

        /// <summary>Legacy single-position overload (used as fallback).</summary>
        public static void Show(Vector3 center)
        {
            Clear();
            _objects.Add(CreateMarker(center));
        }

        /// <summary>Show markers at recorded ball positions (preferred path).</summary>
        public static void Show(IEnumerable<Vector3> positions)
        {
            Clear();
            if (positions == null) return;
            foreach (var pos in positions)
                _objects.Add(CreateMarker(pos));
        }

        /// <summary>Spin the circles every frame (call from OnUpdate).</summary>
        public static void OnUpdate()
        {
            if (_objects.Count == 0) return;
            _angle += Time.deltaTime * 1.5f;
            if (_angle > 360f) _angle -= 360f;
            var rot = Quaternion.Euler(0f, 0f, _angle);
            foreach (var obj in _objects)
                if (obj != null) obj.transform.rotation = rot;
        }

        public static void Clear()
        {
            foreach (var obj in _objects)
                if (obj != null) UnityEngine.Object.Destroy(obj);
            _objects.Clear();
        }

        /// <summary>
        /// Compute ball positions for a floor using the same regular-polygon
        /// layout the game uses in scrLevelMaker / scrPlanet.Update_RefreshAngles.
        /// </summary>
        public static List<Vector3> CalculateBallPositions(scrFloor floor)
        {
            var result = new List<Vector3>();
            var ctl = scrController.instance;
            if (ctl == null) return result;

            int numPlanets = Mathf.Clamp(floor.numPlanets, 2, 8);
            float tileSize = ctl.tileSize;
            bool isCCW = floor.isCCW;
            float entryAngle = (float)floor.entryangle;

            float circum = tileSize / (2f * Mathf.Sin(Mathf.PI / numPlanets));

            double halfInv = scrMisc.GetInverseAnglePerBeatMultiplanet(numPlanets) / 2.0;
            float dirSign = isCCW ? -1f : 1f;
            float centerAngle = entryAngle + dirSign * (float)halfInv;
            Vector3 polyCenter = new Vector3(
                Mathf.Sin(centerAngle) * circum,
                Mathf.Cos(centerAngle) * circum, 0f);

            var floorPos = floor.transform.position;

            for (int i = 0; i < numPlanets; i++)
            {
                float vertAngle = dirSign *
                    (Mathf.PI * 2f * ((1f - i) - numPlanets / 2f) / numPlanets)
                    + centerAngle;
                Vector3 vertex = new Vector3(
                    Mathf.Sin(vertAngle) * circum,
                    Mathf.Cos(vertAngle) * circum, 0f);
                result.Add(floorPos + polyCenter + vertex);
            }

            return result;
        }

        private static GameObject CreateMarker(Vector3 center)
        {
            var go = new GameObject("JEA HitMarker");
            go.transform.position = center;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = new Material(Shader.Find("ADOFAI/ScrollingSprite"));
            lr.material.SetTexture("_MainTex", ADOBase.gc?.planetPolygonTex);
            lr.material.SetVector("_ScrollSpeed", Vector2.zero);
            lr.material.SetFloat("_Time0", 0f);
            lr.textureMode = LineTextureMode.Tile;
            lr.startWidth = 0.07f;
            lr.endWidth = 0.07f;
            var gold = new Color(1f, 0.855f, 0f, 0.9f);
            lr.startColor = gold;
            lr.endColor = gold;
            lr.sortingLayerName = "Floor";
            lr.sortingOrder = 1100;

            float radius = scrController.instance != null
                ? scrController.instance.tileSize * 0.25f : 0.5f;
            const int segments = 48;
            lr.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var a = (float)i / segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(
                    Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
            }

            return go;
        }
    }
}
