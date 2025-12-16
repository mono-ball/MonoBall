# Modding Systems Comparative Analysis: Custom Data Type Patterns

## Executive Summary

This research analyzes how four successful modding ecosystems handle custom data types, examining patterns that could strengthen PokeSharp's modding architecture. The analysis focuses on type declaration, loading, registration, base game interaction, and mod-to-mod compatibility.

**Key Finding**: The most successful modding systems share a common pattern: **Data-Driven Type Definitions + Runtime Registration + Explicit Dependency Resolution**.

---

## 1. Minecraft Forge/Fabric

### Overview
Minecraft's modding ecosystem is one of the most successful, with thousands of mods coexisting through a sophisticated registry system.

### Custom Type Declaration

**Forge Registry System (1.12+)**
```java
// Mods declare custom types through annotated classes
@Mod.EventBusSubscriber(modid = "examplemod", bus = Bus.MOD)
public class ModBlocks {
    @SubscribeEvent
    public static void registerBlocks(RegistryEvent.Register<Block> event) {
        event.getRegistry().register(
            new CustomBlock()
                .setRegistryName("examplemod", "custom_block")
        );
    }
}
```

**Fabric Data-Driven Approach (Modern)**
```json
// data/examplemod/blocks/custom_block.json
{
  "type": "examplemod:custom_block_type",
  "properties": {
    "hardness": 3.0,
    "resistance": 5.0,
    "material": "stone"
  },
  "behavior": "examplemod:custom_behavior"
}
```

### Loading and Registration

**Phase-Based Loading**:
1. **Pre-Init**: Register custom types and capabilities
2. **Init**: Register recipes, handlers, networking
3. **Post-Init**: Cross-mod integration, final setup

**Registry Key Patterns**:
- Namespaced IDs: `modid:typename` (e.g., `minecraft:stone`, `examplemod:custom_ore`)
- Central registry manager tracks all registered objects
- Automatic conflict detection through namespacing
- Registry freezing after loading prevents runtime modifications

### Base Game Interaction

**Extension Points**:
```java
// Forge uses capabilities to extend base game entities
@CapabilityInject(ICustomCapability.class)
public static Capability<ICustomCapability> CUSTOM_CAP = null;

// Attach to vanilla entities
@SubscribeEvent
public static void attachCapabilities(AttachCapabilitiesEvent<Entity> event) {
    if (event.getObject() instanceof PlayerEntity) {
        event.addCapability(
            new ResourceLocation("modid", "custom_data"),
            new CustomCapabilityProvider()
        );
    }
}
```

**Key Patterns**:
- **Capabilities System**: Attach custom data to vanilla objects without modifying them
- **Event Bus**: Mods hook into game events to inject behavior
- **Mixins (Fabric)**: Bytecode modification for surgical changes to vanilla classes
- **Tags**: Group types across mods for interoperability (`forge:ores/copper`)

### Mod-to-Mod Compatibility

**Dependency Resolution**:
```toml
# mods.toml (Forge)
[[dependencies.examplemod]]
    modId="minecraft"
    mandatory=true
    versionRange="[1.19,1.20)"

[[dependencies.examplemod]]
    modId="forge"
    mandatory=true
    versionRange="[41.0.0,)"

[[dependencies.examplemod]]
    modId="othermod"
    mandatory=false  # Optional integration
    versionRange="[2.0,)"
```

**Inter-Mod Communication (IMC)**:
```java
// Send message to another mod
InterModComms.sendTo("othermod", "add_recipe", () -> {
    return new CustomRecipeData(...);
});

// Receive messages from other mods
@SubscribeEvent
public static void handleIMC(InterModEnqueueEvent event) {
    InterModComms.getMessages("examplemod", msg -> msg.method().equals("add_recipe"))
        .forEach(message -> {
            CustomRecipeData data = message.messageSupplier().get();
            // Process the data
        });
}
```

**Compatibility Strategies**:
- **Optional Dependencies**: Check if mod is loaded before accessing its types
- **Reflection API Access**: Safe cross-mod type access without hard dependencies
- **Common Tag System**: Shared categorization (e.g., all copper ores tagged `forge:ores/copper`)
- **Patchouli Integration**: Mods can extend documentation books from other mods

### Strengths
✅ **Namespace Isolation**: Prevents ID conflicts
✅ **Capability System**: Non-invasive extensions to vanilla
✅ **Event-Driven**: Clear extension points
✅ **Strong Tooling**: IDE support, documentation generators
✅ **Versioning**: Semantic versioning with range support

### Weaknesses
❌ **Complexity**: Steep learning curve for modders
❌ **Version Fragility**: Mods break across Minecraft updates
❌ **Performance**: Event bus overhead, capability queries
❌ **Compile-Time Dependencies**: Hard to dynamically load mods

---

## 2. Factorio

### Overview
Factorio's modding system is renowned for its simplicity and power, using a pure data-driven prototype system.

### Custom Type Declaration

**Prototype Pattern**:
```lua
-- prototypes/custom-item.lua
data:extend({
  {
    type = "item",  -- Base prototype type
    name = "my-custom-ore",
    icon = "__my-mod__/graphics/icons/ore.png",
    icon_size = 64,
    subgroup = "raw-resource",
    order = "a[ore]-z[custom]",
    stack_size = 50,
    -- Custom properties
    mining_time = 1.5,
    hardness = 0.8
  }
})
```

**Extending Base Types**:
```lua
-- Create entirely new prototype categories
data.raw["custom-category"] = data.raw["custom-category"] or {}

data.raw["custom-category"]["my-instance"] = {
  type = "custom-category",
  name = "my-instance",
  -- Custom schema here
  custom_field = "value",
  nested = {
    data = 42
  }
}
```

### Loading and Registration

**Three-Phase Loading**:
1. **Settings**: Mod configuration, startup settings
2. **Data**: Prototypes (items, entities, recipes, etc.)
3. **Control**: Runtime scripts and event handlers

**Prototype Stages**:
```lua
-- data.lua (Prototype definition)
data:extend({...})

-- data-updates.lua (Modify other mods' prototypes)
data.raw["item"]["iron-ore"].stack_size = 100

-- data-final-fixes.lua (Final adjustments after all mods)
for _, item in pairs(data.raw["item"]) do
  if item.stack_size then
    item.stack_size = math.min(item.stack_size, 1000)
  end
end
```

**Key Registration Features**:
- **Automatic Merging**: Multiple mods can extend same prototype category
- **Override Order**: Later mods override earlier ones (with `data-updates`)
- **Validation**: Engine validates all prototypes against schema before game starts
- **ID Collision Detection**: Warns about duplicate prototype names

### Base Game Interaction

**Transparent Extension**:
```lua
-- Mods modify base game prototypes directly
local iron_ore = data.raw["item"]["iron-ore"]
iron_ore.stack_size = 200
iron_ore.fuel_value = "2MJ"  -- Add new property

-- Add recipe using vanilla items
data:extend({
  {
    type = "recipe",
    name = "custom-alloy",
    ingredients = {
      {"iron-plate", 2},      -- Vanilla
      {"copper-plate", 3},    -- Vanilla
      {"my-custom-ore", 1}    -- Mod
    },
    result = "custom-alloy-plate"
  }
})
```

**Soft Dependencies**:
```lua
-- info.json
{
  "name": "my-mod",
  "dependencies": [
    "base >= 1.1.0",              // Required
    "? space-exploration >= 0.6", // Optional
    "! incompatible-mod"          // Conflict
  ]
}

-- In data.lua, check if optional mod exists
if mods["space-exploration"] then
  -- Add integration content
  data:extend({
    {
      type = "item",
      name = "space-compatible-item",
      -- Uses space-exploration types
    }
  })
end
```

### Mod-to-Mod Compatibility

**Remote Interfaces** (Runtime API):
```lua
-- Mod A exports interface
remote.add_interface("my-mod", {
  register_ore = function(ore_data)
    table.insert(global.custom_ores, ore_data)
  end,

  get_ore_value = function(ore_name)
    return global.ore_values[ore_name]
  end
})

-- Mod B calls interface
if remote.interfaces["my-mod"] then
  remote.call("my-mod", "register_ore", {
    name = "special-ore",
    value = 100
  })
end
```

**Prototype Access Pattern**:
```lua
-- Check if another mod's prototype exists
if data.raw["item"]["some-mods-item"] then
  -- Safely use or modify it
  local item = data.raw["item"]["some-mods-item"]
  item.stack_size = item.stack_size * 2
end

-- Create compatibility recipes
if mods["bobs-mods"] and mods["angels-mods"] then
  data:extend({
    -- Combined recipe using both mods' items
  })
end
```

**Data Lifecycle Hooks**:
- `data.lua`: First pass at prototype creation
- `data-updates.lua`: Modify prototypes after other mods load
- `data-final-fixes.lua`: Final chance to adjust all prototypes
- `control.lua`: Runtime event handlers and logic

### Strengths
✅ **Simplicity**: Pure Lua data structures, easy to learn
✅ **Data-Driven**: JSON-like tables, no code for data
✅ **Modification Pipeline**: Clear stages for mod interactions
✅ **Live Inspection**: Can inspect all prototypes in-game
✅ **Performance**: Prototypes compiled at load, not runtime
✅ **Deterministic**: No random mod load order issues

### Weaknesses
❌ **Global Namespace**: All prototypes in one namespace (collision risk)
❌ **No Schema Validation**: Custom types not validated until runtime
❌ **Limited Type Safety**: Lua dynamic typing catches errors late
❌ **Prototype Bloat**: All prototype data loaded into memory

---

## 3. Rimworld

### Overview
Rimworld uses XML-based "Defs" (Definitions) for all game content, with C# assemblies for complex behavior.

### Custom Type Declaration

**Def Pattern (XML)**:
```xml
<!-- Defs/CustomItem.xml -->
<?xml version="1.0" encoding="utf-8" ?>
<Defs>
  <ThingDef ParentName="ResourceBase">
    <defName>MyCustomOre</defName>
    <label>custom ore</label>
    <description>A mysterious ore with unusual properties.</description>
    <graphicData>
      <texPath>Things/Item/Resource/CustomOre</texPath>
      <graphicClass>Graphic_Single</graphicClass>
    </graphicData>
    <statBases>
      <MarketValue>15</MarketValue>
      <Mass>0.8</Mass>
    </statBases>
    <!-- Custom properties (extensible) -->
    <comps>
      <li Class="MyMod.CompProperties_CustomBehavior">
        <customValue>42</customValue>
      </li>
    </comps>
  </ThingDef>
</Defs>
```

**Creating New Def Types (C#)**:
```csharp
// Define new Def type
public class CustomDefType : Def
{
    public float customValue;
    public string specialProperty;
    public List<StatModifier> statModifiers;

    // Override to add custom loading/validation
    public override void ResolveReferences()
    {
        base.ResolveReferences();
        // Custom initialization after all defs loaded
    }
}

// XML can now use this type
// <CustomDefType>
//   <defName>MyCustomDef</defName>
//   <customValue>3.14</customValue>
// </CustomDefType>
```

### Loading and Registration

**DefDatabase Pattern**:
```csharp
// Automatic registration of all XML defs
public static class DefDatabase<T> where T : Def, new()
{
    // All defs of type T
    private static List<T> allDefs = new List<T>();

    // Fast lookup by defName
    private static Dictionary<string, T> defsByName = new Dictionary<string, T>();

    // Get def by name
    public static T GetNamed(string defName, bool errorOnFail = true)
    {
        if (defsByName.TryGetValue(defName, out T def))
            return def;

        if (errorOnFail)
            Log.Error($"Could not find def {defName}");
        return null;
    }

    // Get all defs
    public static IEnumerable<T> AllDefs => allDefs;

    // Iterate all defs
    public static IEnumerable<T> AllDefsListForReading => allDefs;
}

// Usage
ThingDef myDef = DefDatabase<ThingDef>.GetNamed("MyCustomOre");
foreach (var building in DefDatabase<ThingDef>.AllDefs.Where(d => d.category == ThingCategory.Building))
{
    // Process all buildings
}
```

**Load Order & Patching**:
```xml
<!-- About/About.xml -->
<ModMetaData>
  <name>My Custom Mod</name>
  <author>Author</author>
  <supportedVersions>
    <li>1.4</li>
  </supportedVersions>
  <loadAfter>
    <li>Ludeon.RimWorld</li>
    <li>Ludeon.RimWorld.Royalty</li>
    <li>OtherMod.CoreContent</li>
  </loadAfter>
  <loadBefore>
    <li>SomeModThatDependsOnMe</li>
  </loadBefore>
</ModMetaData>
```

**XPath Patching System**:
```xml
<!-- Patches/MyPatches.xml -->
<?xml version="1.0" encoding="utf-8" ?>
<Patch>
  <!-- Add to existing def -->
  <Operation Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="Steel"]/statBases</xpath>
    <value>
      <Beauty>2</Beauty>
    </value>
  </Operation>

  <!-- Replace value -->
  <Operation Class="PatchOperationReplace">
    <xpath>Defs/ThingDef[defName="Steel"]/statBases/MarketValue</xpath>
    <value>
      <MarketValue>5.0</MarketValue>
    </value>
  </Operation>

  <!-- Conditional patch (only if mod loaded) -->
  <Operation Class="PatchOperationSequence">
    <operations>
      <li Class="ModCheck">
        <modName>Some Other Mod</modName>
      </li>
      <li Class="PatchOperationAdd">
        <!-- Patch here only runs if "Some Other Mod" is loaded -->
      </li>
    </operations>
  </Operation>
</Patch>
```

### Base Game Interaction

**Inheritance & Extension**:
```xml
<!-- Extend vanilla parent def -->
<ThingDef ParentName="ApparelMakeableBase">
  <defName>MyCustomArmor</defName>
  <!-- Inherits all properties from ApparelMakeableBase -->
  <!-- Override specific properties -->
  <statBases>
    <ArmorRating_Sharp>1.2</ArmorRating_Sharp>
  </statBases>
</ThingDef>

<!-- Create abstract parent for mod content -->
<ThingDef Name="MyModResourceBase" Abstract="True">
  <thingClass>ThingWithComps</thingClass>
  <category>Item</category>
  <resourceReadoutPriority>Middle</resourceReadoutPriority>
  <useHitPoints>false</useHitPoints>
  <selectable>false</selectable>
  <alwaysHaulable>true</alwaysHaulable>
  <drawGUIOverlay>true</drawGUIOverlay>
  <comps>
    <li Class="MyMod.CompProperties_CustomTracking" />
  </comps>
</ThingDef>

<!-- Children inherit all properties -->
<ThingDef ParentName="MyModResourceBase">
  <defName>CustomResource1</defName>
  <!-- ... -->
</ThingDef>
```

**Component System (ThingComps)**:
```csharp
// Attach custom behavior to any Thing
public class CompProperties_CustomBehavior : CompProperties
{
    public float tickInterval = 100f;
    public int maxCharge = 1000;

    public CompProperties_CustomBehavior()
    {
        compClass = typeof(Comp_CustomBehavior);
    }
}

public class Comp_CustomBehavior : ThingComp
{
    private int currentCharge;

    public CompProperties_CustomBehavior Props =>
        (CompProperties_CustomBehavior)props;

    public override void CompTick()
    {
        base.CompTick();
        if (Find.TickManager.TicksGame % Props.tickInterval == 0)
        {
            // Custom behavior every N ticks
        }
    }
}

// XML usage
<ThingDef>
  <defName>CustomThing</defName>
  <comps>
    <li Class="MyMod.CompProperties_CustomBehavior">
      <tickInterval>250</tickInterval>
      <maxCharge>500</maxCharge>
    </li>
  </comps>
</ThingDef>
```

### Mod-to-Mod Compatibility

**Def References Across Mods**:
```xml
<!-- Recipe using items from multiple mods -->
<RecipeDef>
  <defName>CombinedRecipe</defName>
  <ingredients>
    <li>
      <filter>
        <thingDefs>
          <li>Steel</li>              <!-- Vanilla -->
          <li>OtherMod_CustomMetal</li> <!-- Other mod -->
        </thingDefs>
      </filter>
      <count>10</count>
    </li>
  </ingredients>
  <products>
    <MyMod_CombinedAlloy>1</MyMod_CombinedAlloy>
  </products>
</RecipeDef>
```

**Mod Settings & Configuration**:
```csharp
public class MyModSettings : ModSettings
{
    public bool enableFeatureX = true;
    public float difficultyMultiplier = 1.0f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enableFeatureX, "enableFeatureX", true);
        Scribe_Values.Look(ref difficultyMultiplier, "difficultyMultiplier", 1.0f);
    }
}

// Other mods can check settings
if (LoadedModManager.GetMod<MyMod>()?.GetSettings<MyModSettings>()?.enableFeatureX == true)
{
    // Feature is enabled
}
```

**Harmony Patching** (Runtime Code Modification):
```csharp
// Mod can patch any method in vanilla or other mods
[HarmonyPatch(typeof(Thing), "TakeDamage")]
public static class Thing_TakeDamage_Patch
{
    // Runs before original method
    static bool Prefix(Thing __instance, DamageInfo dinfo)
    {
        // Can prevent original method from running
        if (ShouldBlockDamage(__instance, dinfo))
            return false; // Skip original
        return true; // Run original
    }

    // Runs after original method
    static void Postfix(Thing __instance, DamageInfo dinfo, ref DamageWorker.DamageResult __result)
    {
        // Can modify result
        __result.totalDamageDealt *= 1.5f;
    }
}
```

### Strengths
✅ **XML Simplicity**: Easy for non-programmers to create content
✅ **Inheritance**: Reduce boilerplate with parent defs
✅ **XPath Patching**: Surgical modifications without conflicts
✅ **Component System**: Modular, composable behavior
✅ **DefDatabase**: Efficient, type-safe lookups
✅ **Harmony**: Powerful runtime code modification

### Weaknesses
❌ **Verbose XML**: Large mods have thousands of lines of XML
❌ **Limited Validation**: XML errors caught late
❌ **Harmony Conflicts**: Multiple mods patching same method can conflict
❌ **Performance**: Harmony patches have runtime cost
❌ **Opaque Load Order**: Hard to debug mod conflicts

---

## 4. Unity Modding Patterns

### Overview
Unity doesn't have built-in modding support, but several patterns have emerged in the ecosystem (BepInEx, UMM, Asset Bundles, Addressables).

### Custom Type Declaration

**ScriptableObject Pattern**:
```csharp
// Data-driven asset type
[CreateAssetMenu(fileName = "NewCustomData", menuName = "MyMod/CustomData")]
public class CustomDataAsset : ScriptableObject
{
    public string id;
    public string displayName;
    public float baseValue;
    public Sprite icon;
    public CustomBehaviorType behaviorType;

    // Can include validation
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            Debug.LogError($"CustomDataAsset {name} has no ID!");
    }
}

// Create instances in Unity Editor
// Assets/MyMod/Data/CustomData_Example.asset
```

**Asset Bundle Pattern**:
```csharp
// Build-time: Bundle custom assets
[MenuItem("MyMod/Build Asset Bundles")]
static void BuildAssetBundles()
{
    BuildPipeline.BuildAssetBundles(
        "Assets/AssetBundles",
        BuildAssetBundleOptions.None,
        BuildTarget.StandaloneWindows64
    );
}

// Runtime: Load from bundle
AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(modPath, "customassets"));
CustomDataAsset data = bundle.LoadAsset<CustomDataAsset>("CustomData_Example");
```

**Addressables Pattern** (Modern):
```csharp
// Data registered with addressable key
[Addressable("MyMod/CustomData/Example")]
public class CustomDataAsset : ScriptableObject { ... }

// Load asynchronously from anywhere (including remote)
var handle = Addressables.LoadAssetAsync<CustomDataAsset>("MyMod/CustomData/Example");
await handle.Task;
CustomDataAsset data = handle.Result;
```

### Loading and Registration

**MonoBehaviour Mod Manager**:
```csharp
[BepInPlugin("com.author.mymod", "My Mod", "1.0.0")]
[BepInDependency("com.author.othermod", BepInDependency.DependencyFlags.SoftDependency)]
public class MyModPlugin : BaseUnityPlugin
{
    private static CustomTypeRegistry _typeRegistry;

    void Awake()
    {
        // Phase 1: Load data
        _typeRegistry = new CustomTypeRegistry();
        LoadCustomTypes();

        // Phase 2: Register hooks
        On.Player.TakeDamage += Player_TakeDamage_Hook;

        // Phase 3: Integration
        if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.author.othermod"))
        {
            IntegrateWithOtherMod();
        }

        Logger.LogInfo("MyMod loaded successfully!");
    }

    void LoadCustomTypes()
    {
        // Load from JSON config
        string configPath = Path.Combine(Paths.ConfigPath, "MyMod", "custom_types.json");
        var typesJson = File.ReadAllText(configPath);
        var typeList = JsonUtility.FromJson<CustomTypeList>(typesJson);

        foreach (var typeData in typeList.types)
        {
            _typeRegistry.Register(typeData.id, typeData);
        }

        // Load from asset bundles
        string bundlePath = Path.Combine(Paths.PluginPath, "MyMod", "customassets");
        var bundle = AssetBundle.LoadFromFile(bundlePath);
        var assets = bundle.LoadAllAssets<CustomDataAsset>();

        foreach (var asset in assets)
        {
            _typeRegistry.Register(asset.id, asset);
        }
    }
}
```

**Registry Pattern**:
```csharp
public class CustomTypeRegistry
{
    private readonly Dictionary<string, ICustomType> _types = new();
    private readonly List<ICustomType> _allTypes = new();
    private bool _isSealed = false;

    public void Register(string id, ICustomType type)
    {
        if (_isSealed)
            throw new InvalidOperationException("Registry is sealed");

        if (_types.ContainsKey(id))
        {
            Debug.LogWarning($"Overwriting type: {id}");
        }

        _types[id] = type;
        _allTypes.Add(type);
    }

    public void Seal()
    {
        _isSealed = true;
        _allTypes.TrimExcess();
        Debug.Log($"Registry sealed with {_types.Count} types");
    }

    public ICustomType Get(string id)
    {
        return _types.TryGetValue(id, out var type) ? type : null;
    }

    public IEnumerable<ICustomType> GetAll() => _allTypes;

    public IEnumerable<T> GetAll<T>() where T : ICustomType
    {
        return _allTypes.OfType<T>();
    }
}
```

### Base Game Interaction

**MonoMod Hooks**:
```csharp
// Hook into base game methods
public class MyModPlugin : BaseUnityPlugin
{
    void Awake()
    {
        // Method hook (replaces method)
        On.Player.TakeDamage += Player_TakeDamage_Hook;

        // IL hook (modifies IL bytecode - very powerful)
        IL.Player.Update += Player_Update_ILHook;
    }

    // Method hook
    private void Player_TakeDamage_Hook(
        On.Player.orig_TakeDamage orig,
        Player self,
        float damage)
    {
        // Run before
        Debug.Log($"Player taking {damage} damage");

        // Modify damage
        damage *= 0.5f;

        // Call original
        orig(self, damage);

        // Run after
        Debug.Log("Damage applied");
    }

    // IL hook (more complex but powerful)
    private void Player_Update_ILHook(ILContext il)
    {
        var cursor = new ILCursor(il);

        // Find specific instruction
        if (cursor.TryGotoNext(i => i.MatchCallvirt<Rigidbody>("set_velocity")))
        {
            // Inject custom code before velocity set
            cursor.EmitDelegate<Action<Vector3>>((velocity) =>
            {
                Debug.Log($"Setting velocity: {velocity}");
            });
        }
    }
}
```

**Reflection-Based Extension**:
```csharp
// Access private fields/methods from base game
public static class GameObjectExtensions
{
    private static FieldInfo _privateDataField;

    static GameObjectExtensions()
    {
        _privateDataField = typeof(BaseGameClass)
            .GetField("privateData", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    public static CustomData GetModData(this BaseGameClass obj)
    {
        return _privateDataField.GetValue(obj) as CustomData;
    }

    public static void SetModData(this BaseGameClass obj, CustomData data)
    {
        _privateDataField.SetValue(obj, data);
    }
}
```

### Mod-to-Mod Compatibility

**Soft Dependencies (BepInEx)**:
```csharp
[BepInPlugin("com.author.mymod", "My Mod", "1.0.0")]
[BepInDependency("com.author.coremod", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("com.author.optionalmod", BepInDependency.DependencyFlags.SoftDependency)]
public class MyModPlugin : BaseUnityPlugin
{
    void Awake()
    {
        // Check if optional mod is loaded
        if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(
            "com.author.optionalmod", out var pluginInfo))
        {
            Logger.LogInfo($"Found optional mod: {pluginInfo.Metadata.Name}");
            IntegrateWithOptionalMod();
        }
    }

    void IntegrateWithOptionalMod()
    {
        // Use reflection to access other mod's API without hard dependency
        var otherModType = Type.GetType("OtherMod.API, OtherMod");
        if (otherModType != null)
        {
            var registerMethod = otherModType.GetMethod("RegisterCustomType");
            registerMethod?.Invoke(null, new object[] { "MyType", myTypeData });
        }
    }
}
```

**Shared API Pattern**:
```csharp
// API mod (lightweight, just interfaces)
namespace SharedAPI
{
    public interface ICustomTypeProvider
    {
        void RegisterType(string id, ICustomType type);
        ICustomType GetType(string id);
    }

    public interface ICustomType
    {
        string Id { get; }
        string DisplayName { get; }
    }
}

// Core mod implements API
[BepInPlugin("com.author.core", "Core Mod", "1.0.0")]
public class CoreMod : BaseUnityPlugin, ICustomTypeProvider
{
    private CustomTypeRegistry _registry = new();

    void Awake()
    {
        // Expose API to other mods
        ModAPI.RegisterProvider<ICustomTypeProvider>(this);
    }

    public void RegisterType(string id, ICustomType type)
    {
        _registry.Register(id, type);
    }

    public ICustomType GetType(string id)
    {
        return _registry.Get(id);
    }
}

// Other mods use API
[BepInPlugin("com.author.addon", "Addon Mod", "1.0.0")]
[BepInDependency("com.author.core")]
public class AddonMod : BaseUnityPlugin
{
    void Awake()
    {
        var provider = ModAPI.GetProvider<ICustomTypeProvider>();
        provider.RegisterType("addon:custom_type", new MyCustomType());
    }
}
```

### Strengths
✅ **Asset Bundles**: Full Unity editor support for content creation
✅ **ScriptableObjects**: Type-safe, serializable data assets
✅ **MonoMod Hooks**: Powerful runtime code modification
✅ **Reflection**: Can access anything in base game
✅ **BepInEx**: Mature plugin framework with dependency management

### Weaknesses
❌ **No Built-In Support**: Modding is "unofficial"
❌ **Version Fragility**: Unity updates can break mods
❌ **Performance**: Reflection and hooks have overhead
❌ **Complexity**: Requires understanding of Unity internals
❌ **Distribution**: No standardized mod format/repository

---

## 5. Cross-System Pattern Analysis

### Type Declaration Patterns

| System | Pattern | Format | Extensibility |
|--------|---------|--------|---------------|
| **Minecraft** | Code + Annotations | Java/JSON hybrid | High - event system |
| **Factorio** | Pure Data | Lua tables | Very High - full data access |
| **Rimworld** | Data + Code | XML + C# | High - component system |
| **Unity** | Asset-Based | ScriptableObject/JSON | Moderate - reflection needed |
| **PokeSharp (Current)** | Data + Scripts | JSON + C# CSX | High - TypeRegistry + IScriptedType |

**Key Insights**:
- **Best Practice**: Separate data (JSON/XML) from behavior (scripts/components)
- **PokeSharp Alignment**: Already follows this with `ITypeDefinition` (data) + `IScriptedType` (behavior)
- **Gap**: No formal component/capability system for composable behaviors

### Loading and Registration Patterns

| System | Registry Type | Conflict Resolution | Validation |
|--------|---------------|---------------------|------------|
| **Minecraft** | Namespaced Registry | Namespace isolation | At registration |
| **Factorio** | Flat Prototype DB | Last-loaded wins | At game start |
| **Rimworld** | DefDatabase<T> | Patch system | At load + runtime |
| **Unity** | Manual Registry | First-registered wins | Manual/OnValidate |
| **PokeSharp (Current)** | TypeRegistry<T> | Override on duplicate | At JSON load |

**Key Insights**:
- **Best Practice**: Multi-phase loading (data → validation → integration → seal)
- **PokeSharp Gap**: No formal "seal" phase that prevents post-load modifications
- **Recommendation**: Add `TypeRegistry.Seal()` method called after all mods loaded

### Base Game Interaction Patterns

| System | Extension Mechanism | Modification Approach | Safety |
|--------|---------------------|----------------------|--------|
| **Minecraft** | Capabilities, Events | Additive (hooks) | High |
| **Factorio** | Direct Prototype Access | Mutative (data-updates) | Medium |
| **Rimworld** | XPath Patches, Harmony | Hybrid (patches + hooks) | Medium |
| **Unity** | MonoMod Hooks, Reflection | Mutative (IL injection) | Low |
| **PokeSharp (Current)** | JSON Patches, ContentProvider | Hybrid (patches + overrides) | High |

**Key Insights**:
- **Best Practice**: Provide both additive (new content) and patch (modify existing) mechanisms
- **PokeSharp Strength**: ContentProvider priority system safely handles overrides
- **PokeSharp Gap**: No event/hook system for runtime behavior injection

### Mod-to-Mod Compatibility Patterns

| System | Dependency Management | API Exposure | Soft Dependency Support |
|--------|----------------------|--------------|------------------------|
| **Minecraft** | TOML manifest | IMC (Inter-Mod Comms) | Reflection-based checks |
| **Factorio** | JSON manifest | `remote` interfaces | `if mods["name"]` checks |
| **Rimworld** | XML manifest | Static/Harmony access | LoadedModManager checks |
| **Unity** | BepInDependency attribute | Static API classes | Chainloader.PluginInfos |
| **PokeSharp (Current)** | JSON manifest | ModLoader.LoadedMods | Partial (can check if loaded) |

**Key Insights**:
- **Best Practice**: Explicit dependency declaration + runtime API for cross-mod integration
- **PokeSharp Strength**: Good dependency resolution via `ModDependencyResolver`
- **PokeSharp Gap**: No formal API exposure mechanism for mods to provide interfaces to other mods

---

## 6. Recommendations for PokeSharp

### Priority 1: Enhanced Type System (High Impact)

**Problem**: Current system supports custom types within existing categories (BehaviorDefinition, TileBehaviorDefinition) but mods cannot create entirely new type categories.

**Solution**: Type Category Registration

```csharp
// Allow mods to register new type categories
public interface IModdableTypeCategory
{
    string CategoryId { get; }
    Type DefinitionType { get; }
    string ContentFolderKey { get; }
}

public class TypeCategoryRegistry
{
    private readonly Dictionary<string, IModdableTypeCategory> _categories = new();

    public void RegisterCategory<T>(string categoryId, string contentFolderKey)
        where T : ITypeDefinition, new()
    {
        _categories[categoryId] = new TypeCategory<T>(categoryId, contentFolderKey);

        // Dynamically create TypeRegistry<T> for this category
        var registryType = typeof(TypeRegistry<>).MakeGenericType(typeof(T));
        var registry = Activator.CreateInstance(registryType, ...);

        // Store for later use
        _typeRegistries[categoryId] = registry;
    }

    public TypeRegistry<T> GetRegistry<T>() where T : ITypeDefinition
    {
        // Return strongly-typed registry
    }
}

// Mod usage
// In mod's manifest or init script
TypeCategoryRegistry.RegisterCategory<WeatherDefinition>("Weather", "WeatherDefinitions");
TypeCategoryRegistry.RegisterCategory<QuestDefinition>("Quests", "QuestDefinitions");

// ContentProvider automatically picks up the new content folder key
```

**Benefits**:
✅ Mods can create entirely new game systems
✅ No hard-coded type limitations
✅ Follows Factorio's prototype extensibility model

### Priority 2: Component/Capability System (High Impact)

**Problem**: Behavior must be attached via scripts. No composable, data-driven behavior components.

**Solution**: Component Definition System (inspired by Rimworld's comps)

```csharp
// Base component interface
public interface ITypeComponent
{
    string ComponentType { get; }
    void Initialize(ITypeDefinition owner);
    void OnRegistered();
}

// Example component
public class TimedEffectComponent : ITypeComponent
{
    public string ComponentType => "TimedEffect";
    public float Duration { get; set; }
    public string EffectId { get; set; }

    public void Initialize(ITypeDefinition owner) { }
    public void OnRegistered()
    {
        // Register effect timer, etc.
    }
}

// Enhanced type definition
public record BehaviorDefinition : IScriptedType
{
    // Existing properties...

    // NEW: Composable components
    public List<ITypeComponent> Components { get; init; } = new();
}

// JSON usage
{
  "id": "advanced_patrol",
  "displayName": "Advanced Patrol",
  "behaviorScript": "patrol.csx",
  "components": [
    {
      "componentType": "TimedEffect",
      "duration": 5.0,
      "effectId": "speed_boost"
    },
    {
      "componentType": "SoundEmitter",
      "soundId": "footsteps",
      "interval": 1.0
    }
  ]
}
```

**Benefits**:
✅ Modular, reusable behavior without scripts
✅ Data-driven composition
✅ Follows Rimworld's successful component pattern

### Priority 3: Mod API Exposure System (Medium Impact)

**Problem**: Mods cannot easily expose functionality to other mods. No standard way for cross-mod integration.

**Solution**: Mod API Registry (inspired by Factorio's `remote` interfaces)

```csharp
public interface IModApi
{
    string ModId { get; }
    string ApiVersion { get; }
}

public class ModApiRegistry
{
    private readonly Dictionary<string, IModApi> _apis = new();

    public void RegisterApi<T>(string modId, T api) where T : class, IModApi
    {
        _apis[modId] = api;
        _logger.LogInformation($"Mod '{modId}' registered API: {typeof(T).Name}");
    }

    public T? GetApi<T>(string modId) where T : class, IModApi
    {
        if (_apis.TryGetValue(modId, out var api))
        {
            return api as T;
        }
        return null;
    }

    public bool TryGetApi<T>(string modId, out T? api) where T : class, IModApi
    {
        api = GetApi<T>(modId);
        return api != null;
    }
}

// Example: Core mod exposes API
public interface IQuestSystemApi : IModApi
{
    void RegisterQuestType(string typeId, QuestDefinition definition);
    void TriggerQuest(string questId, Entity target);
}

public class QuestSystemApi : IQuestSystemApi
{
    public string ModId => "core:quest-system";
    public string ApiVersion => "1.0.0";

    public void RegisterQuestType(string typeId, QuestDefinition definition)
    {
        _questRegistry.Register(typeId, definition);
    }

    public void TriggerQuest(string questId, Entity target)
    {
        _questManager.StartQuest(questId, target);
    }
}

// In quest system mod initialization
ModApiRegistry.RegisterApi("core:quest-system", new QuestSystemApi());

// Other mod usage
if (ModApiRegistry.TryGetApi<IQuestSystemApi>("core:quest-system", out var questApi))
{
    questApi.RegisterQuestType("mymod:custom_quest", myQuestDef);
}
```

**Benefits**:
✅ Safe cross-mod integration
✅ Version-aware APIs
✅ Optional dependencies work seamlessly

### Priority 4: Registry Sealing & Validation (Low Impact, High Safety)

**Problem**: TypeRegistry can be modified at any time, potentially after systems are initialized.

**Solution**: Sealed Registry Pattern

```csharp
public class TypeRegistry<T> where T : ITypeDefinition
{
    private bool _isSealed = false;

    public void Register(T definition)
    {
        if (_isSealed)
            throw new InvalidOperationException(
                $"Cannot register type '{definition.Id}' - registry is sealed");

        // existing registration logic...
    }

    public void Seal()
    {
        if (_isSealed)
            return;

        _isSealed = true;

        // Validate all registered types
        foreach (var def in _definitions.Values)
        {
            ValidateDefinition(def);
        }

        // Optimize internal structures
        _definitions.TrimExcess();

        _logger.LogInformation(
            $"TypeRegistry<{typeof(T).Name}> sealed with {Count} types");
    }

    private void ValidateDefinition(T definition)
    {
        // Check for missing required properties
        if (string.IsNullOrWhiteSpace(definition.Id))
            throw new InvalidDataException("Type ID cannot be empty");

        // Type-specific validation
        if (definition is IScriptedType scripted)
        {
            if (!string.IsNullOrEmpty(scripted.BehaviorScript))
            {
                // Validate script exists
                if (!HasScript(definition.Id))
                    _logger.LogWarning($"Type '{definition.Id}' references script but none loaded");
            }
        }
    }
}

// In initialization pipeline (after LoadGameDataStep)
public class SealRegistriesStep : IInitializationStep
{
    public string Name => "Seal Type Registries";

    public async Task ExecuteAsync(InitializationContext context)
    {
        // Seal all registries to prevent modifications
        context.BehaviorRegistry.Seal();
        context.TileBehaviorRegistry.Seal();
        // ... seal all other registries

        _logger.LogInformation("All type registries sealed");
    }
}
```

**Benefits**:
✅ Prevents accidental post-init modifications
✅ Validation catches errors early
✅ Performance optimization opportunity

### Priority 5: Type Inheritance/Templates (Medium Impact)

**Problem**: Mods must redefine all properties even when creating similar types.

**Solution**: Parent/Template System (inspired by Rimworld's ParentName)

```json
// Base template definition
{
  "id": "base:npc_behavior",
  "displayName": "Base NPC Behavior",
  "abstract": true,  // Not directly usable, only as parent
  "defaultSpeed": 4.0,
  "pauseAtWaypoint": 1.0,
  "allowInteractionWhileMoving": false
}

// Child definition inherits all properties
{
  "id": "patrol",
  "displayName": "Patrol Behavior",
  "parent": "base:npc_behavior",  // Inherit from parent
  "behaviorScript": "patrol.csx",
  // Only override specific properties
  "defaultSpeed": 3.0  // Slower than base
}

// Child inherits: pauseAtWaypoint=1.0, allowInteractionWhileMoving=false, behaviorScript
```

**Implementation**:
```csharp
public record BehaviorDefinition : ITypeDefinition
{
    // NEW: Parent reference
    [JsonPropertyName("parent")]
    public string? ParentId { get; init; }

    [JsonPropertyName("abstract")]
    public bool IsAbstract { get; init; }

    // Existing properties...
}

// In TypeRegistry.RegisterFromJsonAsync
public async Task RegisterFromJsonAsync(string jsonPath)
{
    T? definition = JsonSerializer.Deserialize<T>(json, options);

    // Check if this type has a parent
    if (definition is IInheritableType inheritable && !string.IsNullOrEmpty(inheritable.ParentId))
    {
        // Apply parent properties first
        var parent = Get(inheritable.ParentId);
        if (parent != null)
        {
            definition = MergeWithParent(definition, parent);
        }
    }

    _definitions[definition.Id] = definition;
}
```

**Benefits**:
✅ Reduces boilerplate in mod content
✅ Establishes clear type hierarchies
✅ Makes mod content more maintainable

---

## 7. Implementation Roadmap

### Phase 1: Foundation (1-2 weeks)
- [ ] Add `TypeCategoryRegistry` for extensible type system
- [ ] Implement `ITypeComponent` interface and component loading
- [ ] Add `TypeRegistry.Seal()` and validation

### Phase 2: Cross-Mod Support (1 week)
- [ ] Implement `ModApiRegistry` for mod API exposure
- [ ] Add API discovery to `ModLoader`
- [ ] Create documentation for mod API best practices

### Phase 3: Enhanced Features (1 week)
- [ ] Implement parent/template inheritance system
- [ ] Add JSON schema validation
- [ ] Create migration guide for existing mods

### Phase 4: Developer Experience (Ongoing)
- [ ] Create example mods demonstrating patterns
- [ ] Write comprehensive modding guide
- [ ] Build VSCode extension for type autocomplete
- [ ] Set up mod validation tools

---

## 8. Comparison Matrix

| Feature | Minecraft | Factorio | Rimworld | Unity | PokeSharp (Current) | PokeSharp (Proposed) |
|---------|-----------|----------|----------|-------|---------------------|---------------------|
| **Namespaced Types** | ✅ modid:type | ❌ Flat | ⚠️ DefNames only | ⚠️ Manual | ✅ mod.json ID | ✅ Enhanced |
| **Data-Driven Types** | ⚠️ Hybrid | ✅ Lua Tables | ✅ XML Defs | ⚠️ ScriptableObjects | ✅ JSON | ✅ Enhanced JSON |
| **Extensible Type Categories** | ✅ Registry | ✅ Prototypes | ⚠️ Fixed Def types | ✅ Manual | ❌ Fixed | ✅ TypeCategoryRegistry |
| **Component System** | ⚠️ Capabilities | ❌ None | ✅ ThingComps | ✅ MonoBehaviour | ❌ None | ✅ ITypeComponent |
| **Parent/Inheritance** | ❌ None | ⚠️ Prototype copying | ✅ ParentName | ⚠️ Prefab variants | ❌ None | ✅ Parent reference |
| **Patch System** | ❌ Must override | ✅ data-updates | ✅ XPath patches | ❌ IL injection | ✅ JSON Patches | ✅ Enhanced |
| **Mod API Exposure** | ✅ IMC | ✅ `remote` | ⚠️ Static access | ⚠️ Manual | ❌ None | ✅ ModApiRegistry |
| **Dependency Management** | ✅ Version ranges | ✅ Soft deps | ✅ Load order | ✅ BepInDependency | ✅ Dependencies | ✅ Enhanced |
| **Registry Validation** | ✅ At registration | ✅ At game start | ⚠️ Partial | ❌ Manual | ⚠️ At JSON load | ✅ Seal + Validate |
| **Content Override** | ⚠️ Replace | ✅ Priority | ✅ Patches | ⚠️ Manual | ✅ Priority | ✅ Priority |

**Legend**:
- ✅ Well-supported
- ⚠️ Partially supported or complex
- ❌ Not supported

---

## 9. Architectural Patterns Summary

### What PokeSharp Already Does Well

1. **Separation of Data and Behavior**
   - `ITypeDefinition` for pure data
   - `IScriptedType` for scriptable behavior
   - Similar to Rimworld's Def + ThingComp pattern

2. **Content Provider Priority System**
   - Mod content overrides base game cleanly
   - Similar to Factorio's mod load order
   - Better than Minecraft's namespace-only isolation

3. **TypeRegistry<T> Pattern**
   - Type-safe, generic registry
   - O(1) lookups like Rimworld's DefDatabase
   - Better performance than Minecraft's event-based system

4. **JSON Patch System**
   - Non-destructive content modification
   - Similar to Rimworld's XPath patches
   - More maintainable than Minecraft's code-based overrides

### What PokeSharp Should Adopt

1. **From Factorio**: Extensible prototype categories
   - Let mods define entirely new type systems
   - No hardcoded type limitations

2. **From Rimworld**: Component/capability composition
   - Modular, data-driven behavior
   - Avoids script proliferation

3. **From Minecraft**: Formal API exposure
   - Cross-mod integration without hard dependencies
   - Version-aware API contracts

4. **From All**: Multi-phase loading with sealing
   - Clear lifecycle stages
   - Validation before systems activate

---

## 10. Conclusion

PokeSharp's current modding architecture is **solid and well-designed**, already incorporating many best practices from successful modding systems:

**Strengths**:
- Clean data/behavior separation
- Type-safe registries
- Priority-based content resolution
- JSON-based patches

**Opportunities for Enhancement**:
1. **Extensible Type System** (high priority): Allow mods to create new type categories
2. **Component System** (high priority): Enable data-driven behavior composition
3. **Mod API Registry** (medium priority): Formalize cross-mod integration
4. **Registry Sealing** (low priority, high safety): Prevent post-init modifications
5. **Type Inheritance** (medium priority): Reduce boilerplate via templates

By implementing these enhancements, PokeSharp will match or exceed the modding capabilities of the most successful systems while maintaining its clean architectural design.

---

## Appendix A: Real-World Examples

### Example 1: Quest System Mod

**Current Approach (Limited)**:
```json
// Can add quest data to existing TileBehaviors or NPCs, but no dedicated quest system
{
  "id": "quest_npc",
  "displayName": "Quest Giver",
  "behaviorScript": "quest_giver.csx"  // All logic in script
}
```

**Proposed Approach (Enhanced)**:
```csharp
// 1. Register new type category
TypeCategoryRegistry.RegisterCategory<QuestDefinition>("Quests", "QuestDefinitions");

// 2. Define quest type with components
public record QuestDefinition : ITypeDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }

    public List<QuestObjective> Objectives { get; init; } = new();
    public List<QuestReward> Rewards { get; init; } = new();
    public List<ITypeComponent> Components { get; init; } = new();
}

// 3. Create quest in JSON
{
  "id": "defeat_team_rocket",
  "displayName": "Defeat Team Rocket",
  "description": "Stop Team Rocket's evil plans",
  "objectives": [
    { "type": "DefeatTrainer", "trainerId": "rocket_grunt_1" },
    { "type": "DefeatTrainer", "trainerId": "rocket_grunt_2" }
  ],
  "rewards": [
    { "type": "Item", "itemId": "master_ball", "quantity": 1 }
  ],
  "components": [
    {
      "componentType": "QuestTimer",
      "duration": 3600  // 1 hour time limit
    }
  ]
}

// 4. Expose quest API for other mods
public interface IQuestSystemApi : IModApi
{
    void RegisterQuestType(string typeId, QuestDefinition definition);
    void StartQuest(string questId, Entity target);
}
```

### Example 2: Weather System Mod

**Current Approach (Hacky)**:
```csharp
// Must piggyback on TileBehavior or create NPC with weather script
{
  "id": "rain_controller",
  "displayName": "Rain Controller",
  "behaviorScript": "weather_rain.csx"
}
```

**Proposed Approach (Clean)**:
```csharp
// 1. Register weather type category
TypeCategoryRegistry.RegisterCategory<WeatherDefinition>("Weather", "WeatherDefinitions");

// 2. Define parent template
{
  "id": "base:weather",
  "abstract": true,
  "transitionDuration": 5.0,
  "components": [
    { "componentType": "SoundEmitter" },
    { "componentType": "ParticleEffect" }
  ]
}

// 3. Create specific weather types
{
  "id": "rain",
  "parent": "base:weather",
  "displayName": "Rain",
  "components": [
    {
      "componentType": "SoundEmitter",
      "soundId": "rain_loop",
      "volume": 0.7
    },
    {
      "componentType": "ParticleEffect",
      "particleId": "rain_drops",
      "density": 100
    },
    {
      "componentType": "EncounterModifier",
      "encounterRateMultiplier": 1.5,
      "typeBoosts": ["water"]
    }
  ]
}

// 4. Other mod adds snow (inherits from rain, overrides particles)
{
  "id": "snow",
  "parent": "rain",  // Inherits sound, encounter system
  "displayName": "Snow",
  "components": [
    {
      "componentType": "ParticleEffect",
      "particleId": "snow_flakes",  // Override particle
      "density": 80
    }
  ]
}
```

---

## Appendix B: Migration Guide

### For Existing Mod Authors

**Old Pattern**:
```json
{
  "id": "custom_behavior",
  "displayName": "Custom Behavior",
  "behaviorScript": "my_behavior.csx"
}
```

**New Pattern (Backward Compatible)**:
```json
{
  "id": "custom_behavior",
  "displayName": "Custom Behavior",
  "behaviorScript": "my_behavior.csx",  // Still works!

  // NEW: Add components for data-driven features
  "components": [
    {
      "componentType": "SoundEmitter",
      "soundId": "footsteps"
    }
  ]
}
```

**Gradual Migration**:
1. Existing mods continue to work (backward compatibility)
2. New features are opt-in (add components, use APIs)
3. Old script-based approach remains valid for complex behavior
4. Can mix: data-driven components + custom scripts

---

## Appendix C: Type Definition Schema

### Proposed JSON Schema for Enhanced Types

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "id": {
      "type": "string",
      "description": "Unique identifier for this type"
    },
    "displayName": {
      "type": "string",
      "description": "Human-readable name"
    },
    "description": {
      "type": "string",
      "description": "Optional description"
    },
    "parent": {
      "type": "string",
      "description": "Parent type ID to inherit from"
    },
    "abstract": {
      "type": "boolean",
      "default": false,
      "description": "If true, cannot be instantiated directly"
    },
    "components": {
      "type": "array",
      "description": "Composable behavior components",
      "items": {
        "type": "object",
        "properties": {
          "componentType": {
            "type": "string",
            "description": "Component type identifier"
          }
        },
        "required": ["componentType"],
        "additionalProperties": true
      }
    }
  },
  "required": ["id", "displayName"]
}
```

---

**Document Version**: 1.0
**Last Updated**: 2025-12-15
**Research By**: Claude Code Research Agent
**Status**: ✅ Complete
