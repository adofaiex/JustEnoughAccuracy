using UnityEngine;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Hit-position marker: a single gold dashed circle drawn at the tile the
    /// player selected in the previewer. Unlike <see cref="DeathMarker"/> there is
    /// only ever one, and it is replaced (not stacked) each time the player clicks
    /// another hit — the previous selection's marker is destroyed.
    /// </summary>
    public static class HitMarker
    {
        private static GameObject? _object;

        public static void Show(Vector3 center)
        {
            Clear();
            _object = CreateMarker(center);
        }

        public static void Clear()
        {
            if (_object != null)
            {
                UnityEngine.Object.Destroy(_object);
                _object = null;
            }
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