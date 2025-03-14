﻿using System;
using System.Collections.Generic;
using System.IO;
using DebugModPlus.Savestates;
using MonsterLove.StateMachine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NineSolsAPI.Utils;
using UnityEngine;

namespace DebugModPlus;

public class Savestate {
    public string? Scene;
    public Vector3? PlayerPosition;
    public string? LastTeleportId;
    public List<MonoBehaviourSnapshot>? MonobehaviourSnapshots;
    public List<MonsterLoveFsmSnapshot>? FsmSnapshots;
    public List<GeneralFsmSnapshot>? GeneralFsmSnapshots;
    public List<ReferenceFixups>? ReferenceFixups;
    public JObject? Flags;

    public void SerializeTo(StreamWriter writer) {
        JsonSerializer.Create(jsonSettings).Serialize(writer, this);
    }

    public static Savestate DeserializeFrom(StreamReader reader) {
        var jsonReader = new JsonTextReader(reader);
        return JsonSerializer.Create(jsonSettings).Deserialize<Savestate>(jsonReader) ??
               throw new Exception("Failed to deserialize savestate");
    }

    public string Serialize() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public static Savestate Deserialize(string data) => JsonConvert.DeserializeObject<Savestate>(data) ??
                                                        throw new Exception("Failed to deserialize savestate");

    private static JsonSerializerSettings jsonSettings = new() {
        Formatting = Formatting.Indented,
        Converters = [new Vector3Converter()],
    };
}

public class MonoBehaviourSnapshot {
    public required string Path;
    public required JToken Data;

    public static MonoBehaviourSnapshot Of(Component mb) => new() {
        Path = ObjectUtils.ObjectComponentPath(mb),
        Data = SnapshotSerializer.Snapshot(mb),
    };
}

public class GeneralFsmSnapshot {
    public required string Path;
    public required string CurrentState;

    public static GeneralFsmSnapshot Of(StateMachineOwner owner) => new() {
        Path = ObjectUtils.ObjectPath(owner.gameObject),
        CurrentState = owner.FsmContext.fsm.State.name,
    };
}

public class MonsterLoveFsmSnapshot {
    public required string Path;
    public required object CurrentState;

    public static MonsterLoveFsmSnapshot Of(IStateMachine machine) => new() {
        Path = ObjectUtils.ObjectPath(machine.Component.gameObject),
        CurrentState = machine.CurrentStateMap.stateObj,
    };
}

public record ReferenceFixupField(string Field, string? Reference);

public class ReferenceFixups {
    public required string Path;
    public required List<ReferenceFixupField> Fields;

    public static ReferenceFixups Of(MonoBehaviour mb, List<ReferenceFixupField> fixups) => new() {
        Path = ObjectUtils.ObjectComponentPath(mb),
        Fields = fixups,
    };
}