using UnityEngine;
using UnityEngine.Rendering;

internal static class ConstructionSmoke
{
    private static Material dustMaterial;

    internal static void Play(Transform parent, Bounds lot, float duration)
    {
        if (dustMaterial == null) dustMaterial = Resources.Load<Material>("GS2_Poeira");
        if (dustMaterial == null) return;

        var cloud = new GameObject("Poeira da construção");
        cloud.SetActive(false);
        cloud.transform.position = new Vector3(lot.center.x, lot.max.y + 0.08f, lot.center.z);
        cloud.transform.SetParent(parent, true);

        var particles = cloud.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = Mathf.Max(0.15f, duration);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = 34;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.24f, 0.58f);
        float size = Mathf.Clamp(Mathf.Max(lot.size.x, lot.size.z) * 0.54f, 0.48f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.82f, 0.67f, 0.49f, 0.72f), new Color(0.96f, 0.84f, 0.65f, 0.82f));

        var emission = particles.emission;
        emission.rateOverTime = 23f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.7f, lot.size.x * 0.85f), 0.15f,
            Mathf.Max(0.7f, lot.size.z * 0.85f));

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.48f, 0.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        var fade = particles.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.17f),
                new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        cloud.SetActive(true);
        particles.Play();
    }
}
