using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.VFX
{
    /// <summary>One-shot particle effect: plays when taken from the pool and goes back to the pool once all particles are gone.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class PooledParticleEffect : MonoBehaviour
    {
        private ParticleSystem particles;

        private void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            var main = particles.main;
            main.stopAction = ParticleSystemStopAction.Callback;
        }

        private void OnEnable()
        {
            particles.Clear(true);
            particles.Play(true);
        }

        private void OnParticleSystemStopped()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
