using BepInEx;

namespace JustEnoughAccuracy.Loaders
{
    [BepInPlugin(ModId, "JustEnoughAccuracy", "0.1.8")]
    [BepInProcess("A Dance of Fire and Ice.exe")]
    public class AdofaiBepInPlugin : BaseUnityPlugin
    {
        private const string ModId = "JEA";
        private BepInHandler? _handler;

        private void Awake()
        {
            _handler = new BepInHandler(Logger);
            Main.Initialize(_handler);
            _handler.TriggerToggle(true);
        }

        private void Update()
        {
            _handler?.TriggerUpdate(UnityEngine.Time.deltaTime);
        }

        private void OnGUI()
        {
            _handler?.TriggerGUI();
        }
    }
}
