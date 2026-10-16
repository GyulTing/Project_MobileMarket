using System;
using System.Collections;
using UnityEngine;
using MobileMarket.Environment;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// The bunker security AI. Starts when the player steps into the start zone, then runs three phases. In each phase it
    /// attacks (phase 1: 20 mm curtains; 2: + laser-guided 40 mm strikes; 3: + rocket rain); every attack heats it up, and
    /// when the heat is full it opens its vents for a limited time. Breaking every vent moves to the next phase (after the
    /// third, the boss is destroyed); running out of time closes them again, but vents already broken this phase stay broken.
    /// </summary>
    public class BossController : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private BossStartZone startZone;
        [Tooltip("Floodlights that switch on one by one when the fight starts; the first attack waits for them.")]
        [SerializeField] private ArenaLights arenaLights;
        [SerializeField] private BossVents vents;
        [SerializeField] private BossGun20[] guns20 = Array.Empty<BossGun20>();
        [SerializeField] private BossCannon40[] cannons40 = Array.Empty<BossCannon40>();
        [SerializeField] private BossRocketPod[] rocketPods = Array.Empty<BossRocketPod>();
        [SerializeField] private TargetLaser[] lasers = Array.Empty<TargetLaser>();

        [Header("Heat (vents open when full)")]
        [SerializeField] private float heatMax = 100f;
        [SerializeField] private float heatSweep = 12f;
        [SerializeField] private float heatFan = 10f;
        [SerializeField] private float heatCrossfire = 16f;
        [SerializeField] private float heatStrike = 14f;
        [SerializeField] private float heatSalvo = 18f;

        [Header("Timing")]
        [SerializeField] private float coolingTime = 20f;
        [SerializeField] private float pauseBetweenPatterns = 1f;
        [SerializeField] private float strikeInterval = 4.5f;
        [SerializeField] private float salvoInterval = 7f;
        [SerializeField] private float startDelay = 1.5f;

        [Header("Defeat")]
        [Tooltip("Death sequence: alarm, chain of blasts, final explosion into the wrecked model.")]
        [SerializeField] private BossDestruction destruction;

        public event Action OnFightStarted;
        public event Action<int> OnPhaseChanged;
        /// <summary>Heat 0..1.</summary>
        public event Action<float> OnHeatChanged;
        /// <summary>Vents opened; the argument is the time limit in seconds.</summary>
        public event Action<float> OnCoolingStarted;
        public event Action OnCoolingEnded;
        public event Action OnDefeated;

        public int Phase { get; private set; }
        public float Heat { get; private set; }
        public float CoolingTimeLeft { get; private set; }
        public bool IsCooling { get; private set; }
        public bool IsDefeated { get; private set; }

        private Transform player;
        private bool attacking;
        private int runningSideAttacks;
        private WaitForSeconds patternPause, startWait;

        private void Awake()
        {
            patternPause = new WaitForSeconds(pauseBetweenPatterns);
            startWait = new WaitForSeconds(startDelay);
        }

        private void OnEnable() { if (startZone != null) startZone.OnPlayerEntered += HandleStart; }
        private void OnDisable() { if (startZone != null) startZone.OnPlayerEntered -= HandleStart; }

        private void HandleStart(Transform who)
        {
            if (Phase != 0 || IsDefeated) return;
            player = who;
            StartCoroutine(Fight());
        }

        /// <summary>Starts the fight without the start zone (testing).</summary>
        [ContextMenu("Start Fight (test)")]
        public void StartFightForTest()
        {
            if (Phase != 0) return;
            foreach (var m in FactionMember.Active) if (m.Faction == Faction.Player) { HandleStart(m.transform); return; }
        }

        private IEnumerator Fight()
        {
            OnFightStarted?.Invoke();
            if (arenaLights != null) yield return arenaLights.TurnOn();
            yield return startWait;
            for (Phase = 1; Phase <= 3; Phase++)
            {
                OnPhaseChanged?.Invoke(Phase);
                vents.NewPhase();
                while (true)
                {
                    yield return AttackUntilOverheated();
                    yield return Cooling();
                    if (vents.AllBroken) break;
                }
                // Covers come down on their own once the last vent breaks; give that a moment.
                yield return new WaitUntil(() => !vents.IsOpen);
            }
            yield return Defeat();
        }

        // ---------------------------------------------------------------- attacking

        private IEnumerator AttackUntilOverheated()
        {
            SetHeat(0f);
            attacking = true;
            runningSideAttacks = 0;
            if (Phase >= 2 && cannons40.Length > 0) StartCoroutine(StrikeLoop());
            if (Phase >= 3 && rocketPods.Length > 0) StartCoroutine(SalvoLoop());

            while (Heat < heatMax)
            {
                yield return GunPattern();
                yield return patternPause;
            }
            attacking = false;
            yield return new WaitUntil(() => runningSideAttacks == 0);
        }

        /// <summary>One 20 mm pattern: sweep, fan, or both guns at once (crossfire).</summary>
        private IEnumerator GunPattern()
        {
            if (guns20.Length == 0 || player == null) { yield return null; AddHeat(heatMax); yield break; }
            int pick = UnityEngine.Random.Range(0, guns20.Length > 1 ? 3 : 2);
            var gun = guns20[UnityEngine.Random.Range(0, guns20.Length)];
            switch (pick)
            {
                case 0: yield return gun.Sweep(player); AddHeat(heatSweep); break;
                case 1: yield return gun.Fan(player); AddHeat(heatFan); break;
                default:
                    // Crossfire: every gun sweeps at once from its own wall.
                    int running = guns20.Length;
                    foreach (var g in guns20) StartCoroutine(RunAndCount(g.Sweep(player), () => running--));
                    yield return new WaitUntil(() => running == 0);
                    AddHeat(heatCrossfire);
                    break;
            }
        }

        private IEnumerator StrikeLoop()
        {
            runningSideAttacks++;
            int next = 0;
            while (attacking)
            {
                // Wait the interval, but stop at once when the attack phase ends.
                for (float t = 0f; t < strikeInterval && attacking; t += Time.deltaTime) yield return null;
                if (!attacking || player == null) break;
                var cannon = cannons40[next++ % cannons40.Length];
                var laser = NearestLaser(cannon.transform.position);
                // Mix the three 40 mm attacks: laser-marked strike, direct aimed shot, suppressive spray.
                switch (UnityEngine.Random.Range(0, 3))
                {
                    case 0:
                        int shots = Phase >= 3 || UnityEngine.Random.value < 0.4f ? 3 : 1;
                        yield return cannon.Strike(player, laser, shots);
                        break;
                    case 1: yield return cannon.AimedShot(player, laser); break;
                    default: yield return cannon.Spray(player); break;
                }
                AddHeat(heatStrike);
            }
            runningSideAttacks--;
        }

        private IEnumerator SalvoLoop()
        {
            runningSideAttacks++;
            int next = 0;
            while (attacking)
            {
                for (float t = 0f; t < salvoInterval && attacking; t += Time.deltaTime) yield return null;
                if (!attacking || player == null) break;
                yield return rocketPods[next++ % rocketPods.Length].Salvo(player);
                AddHeat(heatSalvo);
            }
            runningSideAttacks--;
        }

        private TargetLaser NearestLaser(Vector3 from)
        {
            TargetLaser best = null; float bestD = float.MaxValue;
            foreach (var l in lasers) { float d = (l.transform.position - from).sqrMagnitude; if (d < bestD) { bestD = d; best = l; } }
            return best;
        }

        private static IEnumerator RunAndCount(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        // ---------------------------------------------------------------- cooling

        private IEnumerator Cooling()
        {
            IsCooling = true;
            vents.OpenAll();
            OnCoolingStarted?.Invoke(coolingTime);
            for (CoolingTimeLeft = coolingTime; CoolingTimeLeft > 0f && !vents.AllBroken; CoolingTimeLeft -= Time.deltaTime)
            {
                SetHeat(heatMax * CoolingTimeLeft / coolingTime);
                yield return null;
            }
            if (!vents.AllBroken) vents.CloseAll();   // out of time: covers down, broken ones stay broken
            CoolingTimeLeft = 0f;
            IsCooling = false;
            OnCoolingEnded?.Invoke();
        }

        // ---------------------------------------------------------------- defeat

        private IEnumerator Defeat()
        {
            IsDefeated = true;
            Phase = 4;
            foreach (var l in lasers) l.Hide();
            if (destruction != null) yield return destruction.Play();
            OnDefeated?.Invoke();
        }

        private void AddHeat(float amount) => SetHeat(Mathf.Min(heatMax, Heat + amount));

        private void SetHeat(float value)
        {
            Heat = value;
            OnHeatChanged?.Invoke(heatMax > 0f ? Heat / heatMax : 0f);
        }
    }
}
