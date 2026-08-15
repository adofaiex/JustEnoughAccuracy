using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace JustEnoughAccuracy
{
    /// <summary>
    /// Per-patch-class lifecycle manager, following the same pattern used by Iridium
    /// (PatchManager + MonoHarmonyBackend). Patches are applied individually via
    /// <see cref="Harmony.CreateClassProcessor"/> and removed individually by tracking
    /// the exact (original, patch method) bindings — never through the parameterless
    /// <c>Harmony.UnpatchAll()</c>, whose signature is not stable across the 0Harmony
    /// versions shipped by different loaders.
    /// </summary>
    public static class PatchManager
    {
        private sealed class PatchDef
        {
            public Type Type = null!;
            public string Name = null!;
            public Func<bool> Condition = null!;
            public List<(MethodBase Original, MethodInfo PatchMethod)> Bindings = new();
        }

        private static readonly List<PatchDef> _definitions = new();
        private static readonly Dictionary<Type, bool> _activePatches = new();
        private static Harmony? _harmony;

        public static Harmony? Harmony => _harmony;

        static PatchManager()
        {
            RegisterPatches();
        }

        /// <summary>Create (once) the Harmony instance owned by this mod.</summary>
        public static void Initialize(string ownerId)
        {
            _harmony ??= new Harmony(ownerId);
        }

        /// <summary>True when the given patch class is currently applied.</summary>
        public static bool IsActive(Type patchType)
            => _activePatches.TryGetValue(patchType, out var active) && active;

        private static void RegisterPatches()
        {
            _definitions.Clear();

            foreach (var type in typeof(Patches).GetNestedTypes(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                bool isPatch;
                try
                {
                    isPatch = type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0;
                }
                catch (Exception error)
                {
                    Main.Handler?.Error($"[PatchManager] Skipping {type.FullName}: cannot read Harmony metadata ({error.Message})");
                    continue;
                }

                if (isPatch)
                    _definitions.Add(new PatchDef
                    {
                        Type = type,
                        Name = type.Name,
                        Condition = () => Main.Settings.Enabled
                    });
            }
        }

        /// <summary>
        /// Bring every registered patch in line with its condition. Called on mod enable
        /// and after settings that gate patches change.
        /// </summary>
        public static void UpdateAllPatches()
        {
            if (_harmony == null) return;

            int ok = 0;
            var failures = new List<string>();

            foreach (var def in _definitions)
            {
                if (UpdateSinglePatch(def))
                    ok++;
                else
                    failures.Add(def.Name);
            }

            if (failures.Count > 0)
                Main.Handler.Error($"[PatchManager] {ok}/{_definitions.Count} ok, failed: {string.Join(", ", failures)}");
        }

        /// <summary>Update a single patch by its class type (incremental updates).</summary>
        public static void UpdatePatchByType(Type patchType)
        {
            if (_harmony == null) return;

            var def = _definitions.Find(d => d.Type == patchType);
            if (def != null)
                UpdateSinglePatch(def);
        }

        /// <summary>
        /// Tear down everything this mod owns. Uses the ID-qualified unpatch which
        /// exists across all supported 0Harmony versions.
        /// </summary>
        public static void UnpatchAll()
        {
            _activePatches.Clear();
            foreach (var def in _definitions)
                def.Bindings.Clear();
            _harmony?.UnpatchAll(_harmony.Id);
        }

        private static bool UpdateSinglePatch(PatchDef def)
        {
            bool shouldBeActive = def.Condition();
            bool trackedActive = IsActive(def.Type);

            if (trackedActive == shouldBeActive)
                return true;

            if (shouldBeActive)
            {
                if (!ApplyPatch(def))
                    return false;
                _activePatches[def.Type] = true;
            }
            else
            {
                RemovePatch(def);
                _activePatches.Remove(def.Type);
            }
            return true;
        }

        private static bool ApplyPatch(PatchDef def)
        {
            if (_harmony == null) return false;

            try
            {
                var originals = _harmony.CreateClassProcessor(def.Type).Patch();
                if (originals == null || originals.Count == 0)
                {
                    Main.Handler.Error($"[PatchManager] {def.Name}: no originals patched");
                    return false;
                }

                var bindings = new List<(MethodBase Original, MethodInfo PatchMethod)>();
                foreach (var original in originals)
                {
                    var info = Harmony.GetPatchInfo(original);
                    if (info == null) continue;

                    foreach (var patch in info.Prefixes)
                        AddBinding(bindings, original, patch);
                    foreach (var patch in info.Postfixes)
                        AddBinding(bindings, original, patch);
                    foreach (var patch in info.Transpilers)
                        AddBinding(bindings, original, patch);
                    foreach (var patch in info.Finalizers)
                        AddBinding(bindings, original, patch);
                }

                def.Bindings = bindings;
                return true;
            }
            catch (Exception error)
            {
                try { RemovePatch(def); }
                catch (Exception cleanupError)
                {
                    Main.Handler.Error($"[PatchManager] Cleanup failed for {def.Name}: {cleanupError.Message}");
                }
                Main.Handler.Error($"[PatchManager] Failed to apply {def.Name}: {error.Message}");
                return false;
            }
        }

        private static void AddBinding(
            List<(MethodBase Original, MethodInfo PatchMethod)> bindings,
            MethodBase original,
            Patch patch)
        {
            if (_harmony != null && patch.owner == _harmony.Id)
                bindings.Add((original, patch.PatchMethod));
        }

        private static void RemovePatch(PatchDef def)
        {
            if (_harmony == null) return;

            foreach (var (original, patchMethod) in def.Bindings)
                _harmony.Unpatch(original, patchMethod);
            def.Bindings.Clear();
        }
    }
}
