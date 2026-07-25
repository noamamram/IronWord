# IronWord

## Ready now (Phase A+B+C core)

Playable typing-defense loop with:

- English + Hebrew training profiles (menu language switch)
- Atmaca single-letter threats
- StormShadow multi-letter threats (2–4 letters), unlock after **30s**, weight grows over time
- Typing → interceptor launch → intercept / impact / score / strikes

## How to run

1. Open in Unity 6 and wait for compile.
2. **IronWord → Setup → Wire Art Prefabs** (creates StormShadow gameplay prefab + wires catalog)
3. Open `Assets/Scenes/IronWord_VerticalSlice.unity`
4. Play → choose language → Start
5. Type marker letters. After ~30s StormShadow cruise missiles appear (type the full sequence).

## Architecture

```
Assets/Game/
  Domain/           pure C# (typing, languages, score, session) + content stubs
  Application/      use-cases + Audio/Analytics/Persistence/Meta stubs
  Simulation/       trajectories, threats, interceptors
  Infrastructure/   Unity input, time, pools
  Presentation/     views, HUD, camera, VFX
  Content/          ScriptableObjects (languages, threats, catalog)
  Bootstrap/        GameInstaller composition root
  Tests/EditMode/   NUnit
```

Dependencies flow inward. Domain never references Unity Presentation.

## Threat tuning

`StormShadow` asset (`Assets/Game/Content/Threats/StormShadow.asset`):

- `Unlock After Seconds` = 30
- `Min/Max Token Length` = 2–4
- `Weight Growth Per Minute` raises spawn chance as the run continues

## Stubs (Phase D scaffolding)

Null implementations ready to wire later:

- `IAudioDirector`
- `IAnalyticsSink`
- `ISaveStore`
- `IMetaProgressionService`
- `IContentCatalog`
