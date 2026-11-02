using System.Collections.Generic;
using UnityEngine;

public class GifKitParticleLoop
{
    readonly List<ParticleSystem> systems = new List<ParticleSystem>();
    readonly List<ParticleSystem.Particle[][]> snapshots = new List<ParticleSystem.Particle[][]>();
    readonly List<ParticleSystem.Particle> buffer = new List<ParticleSystem.Particle>();
    int frames;
    int wraps;
    float step;

    public static GifKitParticleLoop Record(int frames, float step)
    {
        var loop = new GifKitParticleLoop { frames = frames, step = step };
        float longest = 0f;
        foreach (var system in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            if (!system.gameObject.scene.IsValid() || !system.gameObject.activeInHierarchy || !system.isPlaying) continue;
            loop.systems.Add(system);
            longest = Mathf.Max(longest, Lifetime(system.main.startLifetime));
        }

        float length = frames * step;
        loop.wraps = Mathf.Max(1, Mathf.CeilToInt(longest / length));
        int steps = frames * (loop.wraps + 1);
        var roots = new List<ParticleSystem>();
        foreach (var system in loop.systems)
        {
            system.Pause(false);
            loop.snapshots.Add(new ParticleSystem.Particle[steps][]);
            if (IsRoot(system, loop.systems)) roots.Add(system);
        }

        for (int s = 0; s < steps; s++)
        {
            if (s > 0)
                foreach (var root in roots) root.Simulate(step, true, false, false);
            for (int k = 0; k < loop.systems.Count; k++)
            {
                var particles = new ParticleSystem.Particle[loop.systems[k].particleCount];
                loop.systems[k].GetParticles(particles);
                loop.snapshots[k][s] = particles;
            }
        }
        return loop;
    }

    public void Apply(int frame)
    {
        int phase = frame % frames;
        float length = frames * step;
        float early = -step * 0.5f;
        float late = length - step * 0.5f;
        for (int k = 0; k < systems.Count; k++)
        {
            if (systems[k] == null) continue;
            buffer.Clear();
            for (int j = 0; j <= wraps; j++)
            {
                int s = phase + j * frames;
                float time = s * step;
                foreach (var particle in snapshots[k][s])
                {
                    float born = time - (particle.startLifetime - particle.remainingLifetime);
                    if (born >= early && born < late) buffer.Add(particle);
                }
            }
            var array = buffer.ToArray();
            systems[k].SetParticles(array, array.Length);
        }
    }

    public void Release()
    {
        foreach (var system in systems)
            if (system != null) system.Play(false);
    }

    static float Lifetime(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant: return curve.constant;
            case ParticleSystemCurveMode.TwoConstants: return curve.constantMax;
            default: return curve.curveMultiplier;
        }
    }

    static bool IsRoot(ParticleSystem system, List<ParticleSystem> systems)
    {
        for (var parent = system.transform.parent; parent != null; parent = parent.parent)
            if (parent.TryGetComponent<ParticleSystem>(out var owner) && systems.Contains(owner)) return false;
        return true;
    }
}
