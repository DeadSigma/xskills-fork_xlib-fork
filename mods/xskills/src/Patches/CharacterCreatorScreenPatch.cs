using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

public static class CharacterCreatorScreenPatch
{
    public static ClassXpDialog ClassXpWindow;
    public static Type CharacterCreatorDialogType;
    public static ICoreClientAPI ClientApi;
    public static Dictionary<string, double> ClassExpMult =
        new Dictionary<string, double>();

    public static IEnumerable<CodeInstruction> AddClassButtonTranspiler(
    IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        var composeMethod = AccessTools.Method(
            typeof(GuiComposer),
            nameof(GuiComposer.Compose),
            new[] { typeof(bool) }
        );

        if (composeMethod == null) return codes;

        Log?.Invoke($"Compose lookup: {composeMethod}");

        int callIndex = codes.FindLastIndex(code => code.Calls(composeMethod));
        Log?.Invoke($"Compose call index: {callIndex}");

        // The call should be preceded by a composer local and the bool argument.
        if (callIndex >= 0)
        {
            for (int i = Math.Max(0, callIndex - 4); i <= callIndex; i++)
                Log?.Invoke($"IL[{i}]: {codes[i]}");
        }

        if (callIndex < 2)
        {
            Log?.Invoke("Compose call not found near expected instructions; skipping injection.");
            return codes;
        }

        int composerLoadIndex = callIndex - 2;
        Log?.Invoke($"Expected composer load: IL[{composerLoadIndex}] = {codes[composerLoadIndex]}");
        var originalLoad = codes[composerLoadIndex];

        if (!IsLocalLoad(originalLoad))
        {
            Log?.Invoke("Expected instruction was not a local load; skipping button injection.");
            return codes;
        }

        var hookLoad = new CodeInstruction(OpCodes.Ldarg_0);
        hookLoad.labels.AddRange(originalLoad.labels);
        originalLoad.labels.Clear();
        hookLoad.blocks.AddRange(originalLoad.blocks);
        originalLoad.blocks.Clear();

        codes.Insert(composerLoadIndex, hookLoad);
        codes.Insert(
            composerLoadIndex + 1,
            new CodeInstruction(
                OpCodes.Call,
                AccessTools.Method(
                    typeof(CharacterCreatorScreenPatch),
                    nameof(AddButtonBeforeCompose)
                )
            )
        );

        Log?.Invoke("Inserted class-tab button hook.");
        return codes;
    }

    public static Dictionary<string, SkillConfig> SkillConfigs = new Dictionary<string, SkillConfig>();

    private static bool IsLocalLoad(CodeInstruction instruction)
    {
        return instruction.opcode == OpCodes.Ldloc
            || instruction.opcode == OpCodes.Ldloc_S
            || instruction.opcode == OpCodes.Ldloc_0
            || instruction.opcode == OpCodes.Ldloc_1
            || instruction.opcode == OpCodes.Ldloc_2
            || instruction.opcode == OpCodes.Ldloc_3;
    }

    private static void AddButtonBeforeCompose(object dialogObject)
    {
        Log?.Invoke("AddButtonBeforeCompose ran.");

        var tabField = AccessTools.Field(dialogObject.GetType(), "curTab");
        var tabValue = tabField?.GetValue(dialogObject);
        Log?.Invoke($"curTab = {tabValue ?? "<null>"}");

        int tab = tabValue is int value ? value : -1;

        if (tab != 1)
        {
            ClassXpWindow?.TryClose();
            return;
        }

        string classCode = ClientApi?.World.Player.Entity
            .WatchedAttributes.GetString("characterClass");

        ClassXpWindow?.SetText(GetClassExpText(classCode));

        if (ClassXpWindow != null && !ClassXpWindow.IsOpened())
        {
            ClassXpWindow.TryOpen(false);
        }
    }

    public static void AfterClassChanged(object __instance)
    {
        if (__instance is not GuiDialog) return;

        string classCode = ClientApi?.World.Player.Entity
            .WatchedAttributes.GetString("characterClass");

        ClassXpWindow?.SetText(GetClassExpText(classCode));
    }

    public sealed class SkillConfig
    {
        public Dictionary<string, double> classexpmult { get; set; }
            = new Dictionary<string, double>();
    }

    public static string GetClassExpText(string classCode)
    {
        var lines = new List<string>();

        if (classCode != null)
        {
            foreach (var entry in SkillConfigs)
            {
                if (entry.Value.classexpmult != null &&
                    entry.Value.classexpmult.TryGetValue(classCode, out double value))
                {
                    string percent = (value * 100).ToString(
                        "+0.##;-0.##;0",
                        CultureInfo.InvariantCulture
                    );

                    string skillName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                         entry.Key.Replace('_', ' ').Replace('-', ' ')
                    );

                    lines.Add($"{skillName}: {percent}%");
                }
            }
        }

        return lines.Count > 0
            ? string.Join("\n", lines)
            : "No class XP modifiers found";
    }

    public static void AfterCharacterCreatorClosed(object __instance)
    {
        if (CharacterCreatorDialogType?.IsInstanceOfType(__instance) != true)
            return;

        ClassXpWindow?.TryClose();
    }
    public static Action<string> Log;
}

public sealed class ClassXpDialog : GuiDialog
{
    public override string ToggleKeyCombinationCode => "xskills-classxp";
    public override bool Focusable => false;

    public ClassXpDialog(ICoreClientAPI api) : base(api)
    {
        ElementBounds dialogBounds = ElementBounds.FixedSize(340, 480)
            .WithAlignment(EnumDialogArea.RightMiddle);

        ElementBounds textBounds = ElementBounds.Fixed(20, 45, 300, 415);

        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(10);
        bgBounds.BothSizing = ElementSizing.FitToChildren;
        bgBounds.WithChildren(textBounds);

        SingleComposer = api.Gui.CreateCompo("xskills-class-xp", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Class XP multipliers", OnTitleBarClose)
            .AddDynamicText(
                "",
                CairoFont.WhiteDetailText(),
                textBounds,
                "classExpMult"
            )
            .Compose();
    }

    public void SetText(string text)
    {
        SingleComposer.GetDynamicText("classExpMult")?.SetNewText(text);
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
}