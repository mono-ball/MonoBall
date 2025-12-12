namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Battle audio manager implementation.
///     Orchestrates all audio during Pokemon battles.
/// </summary>
public class BattleAudioManager : IBattleAudioManager
{
    private readonly IAudioService _audioService;
    private readonly IPokemonCryManager _pokemonCryManager;
    private readonly Dictionary<string, string> _battleMusicMap;

    private string? _preBattleMusicTrack;
    private bool _isActive;
    private ILoopingSoundHandle? _lowHealthWarningInstance;
    private bool _disposed;

    public BattleAudioManager(
        IAudioService audioService,
        IPokemonCryManager pokemonCryManager)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _pokemonCryManager = pokemonCryManager ?? throw new ArgumentNullException(nameof(pokemonCryManager));

        _battleMusicMap = new Dictionary<string, string>();
        InitializeBattleMusicMap();
    }

    public bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }

    public void StartBattleMusic(BattleType battleType, string? musicName = null)
    {
        if (_disposed)
            return;

        // Store the current music track to restore later
        _preBattleMusicTrack = _audioService.CurrentMusicName;

        // Determine which battle music to play
        string battleMusic = musicName ?? GetBattleMusicForType(battleType);

        // Play battle intro sound
        PlayEncounterSound(GetEncounterType(battleType));

        // Start battle music with fade-in
        _audioService.PlayMusic(battleMusic, loop: true, fadeDuration: 0.5f);

        _isActive = true;
    }

    public void StopBattleMusic(float fadeOutDuration = 1.0f)
    {
        if (_disposed)
            return;

        StopLowHealthWarning();

        _audioService.StopMusic(fadeOutDuration);

        // Restore previous music if there was one
        if (!string.IsNullOrEmpty(_preBattleMusicTrack))
        {
            // Wait for fade out, then restore (in a real implementation, this would be async)
            // For now, we'll just restore immediately after the battle
            _audioService.PlayMusic(_preBattleMusicTrack, loop: true, fadeDuration: fadeOutDuration);
            _preBattleMusicTrack = null;
        }

        _isActive = false;
    }

    public void PlayMoveSound(string moveName, string? moveType = null)
    {
        if (_disposed || !_isActive)
            return;

        // Try to play move-specific sound first
        string moveSoundPath = $"Audio/Battle/Moves/{moveName}";
        if (!_audioService.PlaySound(moveSoundPath))
        {
            // Fall back to type-based sound
            if (!string.IsNullOrEmpty(moveType))
            {
                string typeSoundPath = $"Audio/Battle/Types/{moveType}";
                _audioService.PlaySound(typeSoundPath);
            }
        }
    }

    public void PlayEncounterSound(EncounterType encounterType)
    {
        if (_disposed)
            return;

        string soundPath = encounterType switch
        {
            EncounterType.Wild => "Audio/Battle/Encounter_Wild",
            EncounterType.Trainer => "Audio/Battle/Encounter_Trainer",
            EncounterType.Legendary => "Audio/Battle/Encounter_Legendary",
            _ => "Audio/Battle/Encounter_Wild"
        };

        _audioService.PlaySound(soundPath, volume: 0.9f);
    }

    public void PlayBattleCry(int speciesId, bool isPlayerPokemon)
    {
        if (_disposed || !_isActive)
            return;

        // Adjust pitch slightly based on which side
        float pitch = isPlayerPokemon ? 0.05f : -0.05f;
        _pokemonCryManager.PlayCry(speciesId, volume: 0.85f, pitch: pitch);
    }

    public void PlayUISound(BattleUIAction action)
    {
        if (_disposed)
            return;

        string soundPath = action switch
        {
            BattleUIAction.MenuOpen => "Audio/UI/Menu_Open",
            BattleUIAction.MenuClose => "Audio/UI/Menu_Close",
            BattleUIAction.Select => "Audio/UI/Select",
            BattleUIAction.Back => "Audio/UI/Back",
            BattleUIAction.Invalid => "Audio/UI/Invalid",
            BattleUIAction.TargetSelect => "Audio/UI/Target_Select",
            BattleUIAction.ItemUse => "Audio/Battle/Item_Use",
            BattleUIAction.BallThrow => "Audio/Battle/Ball_Throw",
            BattleUIAction.Run => "Audio/Battle/Run",
            _ => "Audio/UI/Select"
        };

        _audioService.PlaySound(soundPath);
    }

    public void PlayStatusSound(string statusCondition)
    {
        if (_disposed || !_isActive)
            return;

        string soundPath = $"Audio/Battle/Status/{statusCondition}";
        _audioService.PlaySound(soundPath);
    }

    public void PlayLowHealthWarning()
    {
        if (_disposed || !_isActive || _lowHealthWarningInstance != null)
            return;

        _lowHealthWarningInstance = _audioService.PlayLoopingSound(
            "Audio/Battle/Low_Health_Warning",
            volume: 0.6f
        );
    }

    public void StopLowHealthWarning()
    {
        if (_lowHealthWarningInstance == null)
            return;

        _audioService.StopLoopingSound(_lowHealthWarningInstance);
        _lowHealthWarningInstance = null;
    }

    public void Update(float deltaTime)
    {
        if (_disposed || !_isActive)
            return;

        // Update any time-based audio logic here
        // For example, checking if low health warning should still be playing
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopLowHealthWarning();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void InitializeBattleMusicMap()
    {
        // Map battle types to music track names
        // These should be configured based on your game's audio assets
        _battleMusicMap[nameof(BattleType.Wild)] = "Audio/Music/Battle_Wild";
        _battleMusicMap[nameof(BattleType.Trainer)] = "Audio/Music/Battle_Trainer";
        _battleMusicMap[nameof(BattleType.GymLeader)] = "Audio/Music/Battle_Gym_Leader";
        _battleMusicMap[nameof(BattleType.EliteFour)] = "Audio/Music/Battle_Elite_Four";
        _battleMusicMap[nameof(BattleType.Champion)] = "Audio/Music/Battle_Champion";
        _battleMusicMap[nameof(BattleType.Legendary)] = "Audio/Music/Battle_Legendary";
        _battleMusicMap[nameof(BattleType.Rival)] = "Audio/Music/Battle_Rival";
    }

    private string GetBattleMusicForType(BattleType battleType)
    {
        string key = battleType.ToString();
        return _battleMusicMap.TryGetValue(key, out var musicPath)
            ? musicPath
            : "Audio/Music/Battle_Wild"; // Default fallback
    }

    private EncounterType GetEncounterType(BattleType battleType)
    {
        return battleType switch
        {
            BattleType.Wild => EncounterType.Wild,
            BattleType.Legendary => EncounterType.Legendary,
            _ => EncounterType.Trainer
        };
    }
}
