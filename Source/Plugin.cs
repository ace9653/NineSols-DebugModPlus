using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx;
using DebugMod.Source;
using DebugMod.Source.Modules;
using DebugMod.Source.Modules.Hitbox;
using HarmonyLib;
using NineSolsAPI;
using QFSW.QC;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DebugMod;

[HarmonyPatch]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class Patches {
    [HarmonyPatch(typeof(QuantumConsoleProcessor), "LoadCommandsFromType")]
    [HarmonyFinalizer]
    private static Exception LoadCommandsFromType(Type type, Exception __exception) => null;

    [HarmonyPatch(typeof(QuantumConsole), "IsSupportedState")]
    [HarmonyPrefix]
    private static bool IsSupportedState(ref bool __result) {
        __result = true;
        return false;
    }
}

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public static Plugin Instance;

    public KeybindManager KeybindManager;
    private DebugUI debugUI;

    private Harmony harmony;
    private TMP_Text debugCanvasInfoText;

    private InfotextModule infotextModule;
    public HitboxModule HitboxModule = new();
    public SavestateModule SavestateModule = new();

    public void LogInfo(object msg) {
        Logger.LogInfo(msg);
    }


    /*private class DebugModActionSet : PlayerActionSet {
        public PlayerAction ToggleConsole;
        public PlayerAction ToggleSettings;

        public void Initialize() {
            ToggleConsole = CreatePlayerAction("Toggle Console");
            ToggleSettings = CreatePlayerAction("Toggle Settings");

            ToggleConsole.AddDefaultBinding(Key.Control, Key.Period);
            ToggleSettings.AddDefaultBinding(Key.Control, Key.Comma);
        }
    }*/

    private bool consoleInitialized;

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
        Logger.LogInfo($"Scene loaded: {scene.name}");

        if (!consoleInitialized && QuantumConsole.Instance) {
            QuantumConsole.Instance.OnActivate += QuantumConsoleActivate;
            QuantumConsole.Instance.OnDeactivate += QuantumConsoleDeactivate;
        }
        // LoadActionSet();
    }

    private void QuantumConsoleActivate() {
        if (!GameCore.IsAvailable()) return;
        GameCore.Instance.player.playerInput.VoteForState(PlayerInputStateType.Console, QuantumConsole.Instance);
    }

    private void QuantumConsoleDeactivate() {
        if (!GameCore.IsAvailable()) return;
        GameCore.Instance.player.playerInput.RevokeAllMyVote(QuantumConsole.Instance);
    }

    /*private void LoadActionSet() {
        if (actionSet == null && InputManager.IsSetup) {
            actionSet = new DebugModActionSet();
            actionSet.Initialize();
        }
    }*/

    private void Awake() {
        Instance = this;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} started loading...");

        try {
            harmony = Harmony.CreateAndPatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"Patched {harmony.GetPatchedMethods().Count()} methods...");
        } catch (Exception e) {
            Logger.LogError(e);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        KeybindManager = new KeybindManager();
        debugUI = gameObject.AddComponent<DebugUI>();

        KeybindManager.Add(ToggleConsole, KeyCode.LeftControl, KeyCode.Period);
        KeybindManager.Add(ToggleSettings, KeyCode.LeftControl, KeyCode.Comma);

        debugUI.AddBindableMethods(typeof(FreecamModule));
        debugUI.AddBindableMethods(typeof(TimeModule));
        debugUI.AddBindableMethods(typeof(InfotextModule));
        debugUI.AddBindableMethods(typeof(HitboxModule));
        debugUI.AddBindableMethods(typeof(SavestateModule));


        var debugText = new GameObject();
        debugText.transform.SetParent(NineSolsAPICore.FullscreenCanvas.gameObject.transform);
        debugCanvasInfoText = debugText.AddComponent<TextMeshProUGUI>();
        debugCanvasInfoText.alignment = TextAlignmentOptions.TopLeft;
        debugCanvasInfoText.fontSize = 20;
        debugCanvasInfoText.color = Color.white;

        var debugTextTransform = debugCanvasInfoText.GetComponent<RectTransform>();
        debugTextTransform.anchorMin = new Vector2(0, 1);
        debugTextTransform.anchorMax = new Vector2(0, 1);
        debugTextTransform.pivot = new Vector2(0f, 1f);
        debugTextTransform.anchoredPosition = new Vector2(10, -10);
        debugTextTransform.sizeDelta = new Vector2(800f, 0f);
        infotextModule = new InfotextModule(debugCanvasInfoText);

        QuantumConsoleProcessor.GenerateCommandTable(true);

        // LoadActionSet();

        RCGLifeCycle.DontDestroyForever(gameObject);

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void Start() {
        Invoke(nameof(AfterStart), 0);
    }

    private void AfterStart() {
        if (SceneManager.GetActiveScene().name == "Logo") SceneManager.LoadScene("TitleScreenMenu");
    }

    private void ToggleConsole() {
        if (!QuantumConsole.Instance) return;
        //CallPrivateMethod(typeof(PlayerInputBinder), "BindQuantumConsole",GameCore.Instance.player.playerInput);
        QuantumConsole.Instance.Toggle();
    }

    private void ToggleSettings() {
        debugUI.settingsOpen = !debugUI.settingsOpen;
        if (Player.i is not null) {
            // if (settingsOpen) {
            // stateBefore = Player.i.playerInput.fsm.State;
            // Player.i.playerInput.fsm.ChangeState(PlayerInputStateType.Console);
            // } else
            // Player.i.playerInput.fsm.ChangeState(stateBefore);
        }
    }

    private void Update() {
        KeybindManager.Update();

        FreecamModule.Update();
        infotextModule.Update();
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        HitboxModule.Unload();
        SavestateModule.Unload();
        // actionSet.Destroy();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }
}