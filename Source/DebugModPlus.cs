using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BlendModes;
using DebugModPlus.Modules;
using DebugModPlus.Modules.Hitbox;
using HarmonyLib;
using InControl;
using NineSolsAPI;
using QFSW.QC;
using RCGMaker.Core;
using UnityEngine;
using UnityEngine.XR;

namespace DebugModPlus;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class DebugModPlus : BaseUnityPlugin {
    public static DebugModPlus Instance;

    private DebugUI debugUI;
    private QuantumConsoleModule quantumConsoleModule;

    private Harmony harmony;

    private InfotextModule infotextModule;
    public HitboxModule HitboxModule = new();
    public SavestateModule SavestateModule = new();
    public SpeedrunTimerModule SpeedrunTimerModule;
    //FSM Inspector disabled, error handling gets sent through toast and lags game
    //TODO: fix and re-enable
    //TODO: make modules toggleable
    //public FsmInspectorModule FsmInspectorModule;
    public GhostModule GhostModule = new();


    private void Awake() {
        Instance = this;
        Log.Init(Logger);
        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} started loading...");

        try {
            harmony = Harmony.CreateAndPatchAll(typeof(DebugModPlus).Assembly);
            Log.Info($"Patched {harmony.GetPatchedMethods().Count()} methods...");
        } catch (Exception e) {
            Log.Error(e);
        }

        debugUI = gameObject.AddComponent<DebugUI>();
        quantumConsoleModule = new QuantumConsoleModule();
        infotextModule = new InfotextModule();
        SpeedrunTimerModule = new SpeedrunTimerModule();
        //FsmInspectorModule = new FsmInspectorModule();
        GhostModule = new GhostModule();

        SavestateModule.SavestateLoaded += (_, _) => SpeedrunTimerModule.OnSavestateLoaded();
        SavestateModule.SavestateCreated += (_, _) => SpeedrunTimerModule.OnSavestateCreated();


        KeybindManager.Add(this, ToggleConsole, KeyCode.LeftControl, KeyCode.Period);
        KeybindManager.Add(this, ToggleSettings, KeyCode.LeftControl, KeyCode.Comma);
        // KeybindManager.Add(this, () => GhostModule.ToggleRecording(), KeyCode.P);
        // KeybindManager.Add(this, () => GhostModule.Playback(GhostModule.CurrentRecording), KeyCode.O);

        var changeModeShortcut = Config.Bind("SpeedrunTimer", "Change Mode", new KeyboardShortcut());
        var resetTimerShortcut = Config.Bind("SpeedrunTimer", "Reset Timer", new KeyboardShortcut());
        var pauseTimerShortcut = Config.Bind("SpeedrunTimer", "Pause Timer", new KeyboardShortcut());
        var setStartpointShortcut = Config.Bind("SpeedrunTimer", "Set Startpoint", new KeyboardShortcut());
        var setEndpointShortcut = Config.Bind("SpeedrunTimer", "Set Endpoint", new KeyboardShortcut());
        KeybindManager.Add(this, () => SpeedrunTimerModule.CycleTimerMode(), () => changeModeShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.ResetTimer(), () => resetTimerShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.PauseTimer(), () => pauseTimerShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.SetStartpoint(), () => setStartpointShortcut.Value);
        KeybindManager.Add(this, () => SpeedrunTimerModule.SetEndpoint(), () => setEndpointShortcut.Value);

        // var recordGhost = Config.Bind("SpeedrunTimer", "Record Ghost", false);
        // KeybindManager.Add(this, () => SpeedrunTimerModule.CycleTimerMode(), () => changeModeShortcut.Value);


        debugUI.AddBindableMethods(Config, typeof(FreecamModule));
        debugUI.AddBindableMethods(Config, typeof(TimeModule));
        debugUI.AddBindableMethods(Config, typeof(InfotextModule));
        debugUI.AddBindableMethods(Config, typeof(HitboxModule));
        debugUI.AddBindableMethods(Config, typeof(SavestateModule));
        debugUI.AddBindableMethods(Config, typeof(CheatModule));

        RCGLifeCycle.DontDestroyForever(gameObject);

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
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
        FreecamModule.Update();
        MapTeleportModule.Update();
        infotextModule.Update();

        if (Input.GetKey(KeyCode.LeftControl)) {
            Cursor.visible = true;

            //disabling this for now because it caused issues
            if (Input.GetMouseButtonDown(0)) {
                ToastManager.Toast("click");
                try {
                    var mainCamera = CameraManager.Instance.cameraCore.theRealSceneCamera;
                    var worldPosition =
                        mainCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y,
                            -mainCamera.transform.position.z));
                    worldPosition.z = 0; // Set z to 0 to match the 2D plane
                    //var sprites = PickSprite(worldPosition);
                    /*
                    StateMachineOwner? sm = null;
                    foreach (var sprite in sprites) {
                        var smm = sprite.GetComponentInParent<StateMachineOwner>();
                        if (smm) sm = smm;
                    }

                    if (sm) {
                        FsmInspectorModule.ObjectsToDisplay = new List<GameObject> { sm.gameObject };
                        ToastManager.Toast(sm);
                    }*/
                } catch (Exception e) {
                    ToastManager.Toast(e);
                }
                    
            }
        }
    }

    private List<SpriteRenderer> PickSprite(Vector3 worldPosition) {
        List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        var spriteRenderers = FindObjectsOfType<SpriteRenderer>();
        foreach (var spriteRenderer in spriteRenderers) {
            if (!IsWithinSpriteBounds(spriteRenderer, worldPosition)) continue;

            var spriteName = spriteRenderer.gameObject.name.ToLower();
            var parentName = spriteRenderer.gameObject.transform.parent?.name ?? "";
            if (spriteName.Contains("light") || spriteName.Contains("fade") || spriteName.Contains("glow") ||
                spriteName.Contains("attack") ||
                parentName.Contains("Vibe") || parentName.Contains("Skin")) continue;

            sprites.Add(spriteRenderer);
        }

        return sprites;


        bool IsWithinSpriteBounds(SpriteRenderer spriteRenderer, Vector3 position) {
            var bounds = spriteRenderer.bounds;
            return bounds.Contains(position);
        }
    }
    private void LateUpdate() {
        try {
            GhostModule.LateUpdate();
            SpeedrunTimerModule.LateUpdate();
        } catch (Exception e) {
            Log.Error(e);
        }
    }

    private void OnGUI() {
        SpeedrunTimerModule.OnGui();
        //FsmInspectorModule.OnGui();
    }


    private void OnDestroy() {
        harmony.UnpatchSelf();
        HitboxModule.Unload();
        SavestateModule.Unload();
        quantumConsoleModule.Unload();
        GhostModule.Unload();
        SpeedrunTimerModule.Destroy();
        infotextModule.Destroy();

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} unloaded\n\n");
    }
}