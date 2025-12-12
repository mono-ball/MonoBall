using Microsoft.Xna.Framework.Audio;
using MonoBallFramework.Game.Engine.Audio.Configuration;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Pooled sound effect instance manager implementation.
///     Reduces allocations and manages concurrent sound playback limits.
/// </summary>
public class SoundEffectPool : ISoundEffectPool
{
    private readonly List<SoundEffectInstance> _activeInstances;
    private readonly Stack<SoundEffectInstance> _pooledInstances;
    private readonly int _maxConcurrentInstances;
    private bool _disposed;

    public SoundEffectPool(int maxConcurrentInstances = AudioConstants.MaxConcurrentSounds)
    {
        if (maxConcurrentInstances <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentInstances));

        _maxConcurrentInstances = maxConcurrentInstances;
        _activeInstances = new List<SoundEffectInstance>(maxConcurrentInstances);
        _pooledInstances = new Stack<SoundEffectInstance>(maxConcurrentInstances);
    }

    public int MaxConcurrentInstances => _maxConcurrentInstances;

    public int ActiveInstanceCount => _activeInstances.Count;

    public int PooledInstanceCount => _pooledInstances.Count;

    public SoundEffectInstance? PlayPooled(
        SoundEffect soundEffect,
        float volume = 1.0f,
        float pitch = 0f,
        float pan = 0f)
    {
        if (_disposed || soundEffect == null)
            return null;

        var instance = GetOrCreateInstance(soundEffect);
        if (instance == null)
            return null;

        instance.Volume = Math.Clamp(volume, AudioConstants.MinVolume, AudioConstants.MaxVolume);
        instance.Pitch = Math.Clamp(pitch, AudioConstants.MinPitch, AudioConstants.MaxPitch);
        instance.Pan = Math.Clamp(pan, AudioConstants.MinPan, AudioConstants.MaxPan);
        instance.IsLooped = false;

        try
        {
            instance.Play();
            _activeInstances.Add(instance);
            return instance;
        }
        catch
        {
            ReturnInstanceToPool(instance);
            return null;
        }
    }

    public SoundEffectInstance? RentLoopingInstance(SoundEffect soundEffect, float volume = 1.0f)
    {
        if (_disposed || soundEffect == null)
            return null;

        var instance = GetOrCreateInstance(soundEffect);
        if (instance == null)
            return null;

        instance.Volume = Math.Clamp(volume, AudioConstants.MinVolume, AudioConstants.MaxVolume);
        instance.IsLooped = true;

        try
        {
            instance.Play();
            _activeInstances.Add(instance);
            return instance;
        }
        catch
        {
            ReturnInstanceToPool(instance);
            return null;
        }
    }

    public void ReturnInstance(SoundEffectInstance instance)
    {
        if (_disposed || instance == null)
            return;

        instance.Stop();
        _activeInstances.Remove(instance);
        ReturnInstanceToPool(instance);
    }

    public void Update()
    {
        if (_disposed)
            return;

        // Check for stopped instances and return them to the pool
        for (int i = _activeInstances.Count - 1; i >= 0; i--)
        {
            var instance = _activeInstances[i];
            if (instance.State == SoundState.Stopped && !instance.IsLooped)
            {
                _activeInstances.RemoveAt(i);
                ReturnInstanceToPool(instance);
            }
        }
    }

    public void StopAll()
    {
        if (_disposed)
            return;

        foreach (var instance in _activeInstances)
        {
            instance.Stop();
            ReturnInstanceToPool(instance);
        }
        _activeInstances.Clear();
    }

    public void Clear()
    {
        if (_disposed)
            return;

        StopAll();

        foreach (var instance in _pooledInstances)
        {
            instance.Dispose();
        }
        _pooledInstances.Clear();
    }

    public (int active, int pooled, int total) GetPoolStatistics()
    {
        return (_activeInstances.Count, _pooledInstances.Count, _activeInstances.Count + _pooledInstances.Count);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();

        foreach (var instance in _activeInstances)
        {
            instance.Dispose();
        }
        _activeInstances.Clear();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private SoundEffectInstance? GetOrCreateInstance(SoundEffect soundEffect)
    {
        // Try to get from pool first
        if (_pooledInstances.Count > 0)
        {
            return _pooledInstances.Pop();
        }

        // Check if we've hit the limit
        if (_activeInstances.Count >= _maxConcurrentInstances)
        {
            // Find the oldest non-looping instance and steal it
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                var instance = _activeInstances[i];
                if (!instance.IsLooped)
                {
                    instance.Stop();
                    _activeInstances.RemoveAt(i);
                    return instance;
                }
            }

            // All instances are looping, can't steal
            return null;
        }

        // Create a new instance
        try
        {
            return soundEffect.CreateInstance();
        }
        catch
        {
            return null;
        }
    }

    private void ReturnInstanceToPool(SoundEffectInstance instance)
    {
        if (_pooledInstances.Count < _maxConcurrentInstances)
        {
            _pooledInstances.Push(instance);
        }
        else
        {
            // Pool is full, dispose the instance
            instance.Dispose();
        }
    }
}
