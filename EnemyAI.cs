using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static GameEnums;

public enum Faction { Passive, Undead, Demon, Monster, Humanoid, Animals, Mutates, Magical, Human, Re_AnimatedObjects }
public enum EnemyClass { Melee, Ranged, Tank, Mage, Knight }
public enum BossType { None, Commander, Stronghold }

// this is the ai for the enemies in my game any and all contributions are wellcome 
// please dont use delta time and use the tick i used you can see it in the code
// you can use this code for your own projects

[System.Serializable]
public class DeathEffect
{
    public GameObject effectPrefab;
    public float chance;
    public int quantity = 1;
    public Vector2 spawnAreaSize = new Vector2(1f, 1f);
}

public class EnemyAI : Health
{
    [Header("Enemy Class")]
    public EnemyClass classType = EnemyClass.Melee;

    private const float baselineTickRate = 20f;

    [Header("Enemy Settings")]
    public float speed = 3f;
    public float detectionRange = 5f;
    public int contactDamage = 5;
    public float damageInterval = 1f;
    public float knockbackForce = 3f;
    public int expReward = 10;

    [Header("Attack Reach")]
    public CircleCollider2D attackReachCollider;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float projectileCooldown = 2f;
    public float projectileSpawnDistance = 1f;
    public Vector2 projectileSpawnOffset = Vector2.zero;

    [Header("Boss Settings")]

    public BossType bossType = BossType.None;
    public float minionDetectionRange = 50f;
    public float minionFollowRadius = 40f;

    [Header("Commander Settings")]
    public float commanderRetreatDistance = 35f;
    private float commanderRetreatTimer = 0f;
    private bool shouldRetreat = false;

    [Header("Stronghold Settings")]
    public float minionMinDistance = 7f;
    public float minionMaxDistance = 15f;
    private float proximityCheckCooldown = 5f;
    private float lastProximityCheckTime = 0f;

    [Header("Faction Settings")]
    public Faction faction = Faction.Monster;

    [Header("Attack Settings")]
    public GameEnums.DamageType damageType = GameEnums.DamageType.Physical; 
    public GameEnums.ElementType elementType = GameEnums.ElementType.None;

    [Header("Resistance Settings")]
    [Range(0, 1)] public float physicalResistance = 0f;
    [Range(0, 1)] public float magicalResistance = 0f;
    [Range(0, 1)] public float holyResistance = 0f;
    [Range(0, 1)] public float demonicResistance = 0f;

    [Header("Weakness Settings")]  
    [Range(0, 1)] public float physicalWeakness = 0f;
    [Range(0, 1)] public float magicalWeakness = 0f;
    [Range(0, 1)] public float holyWeakness = 0f;
    [Range(0, 1)] public float demonicWeakness = 0f;

    [Header("Elemental Resistances")]
    [Range(0, 1)] public float fireResistance = 0f;
    [Range(0, 1)] public float waterResistance = 0f;
    [Range(0, 1)] public float earthResistance = 0f;
    [Range(0, 1)] public float airResistance = 0f;
    [Range(0, 1)] public float iceResistance = 0f;
    [Range(0, 1)] public float lightningResistance = 0f;
    [Range(0, 1)] public float spaceResistance = 0f;
    [Range(0, 1)] public float timeResistance = 0f;

    [Header("Elemental Weaknesses")]  
    [Range(0, 1)] public float fireWeakness = 0f;
    [Range(0, 1)] public float waterWeakness = 0f;
    [Range(0, 1)] public float earthWeakness = 0f;
    [Range(0, 1)] public float airWeakness = 0f;
    [Range(0, 1)] public float iceWeakness = 0f;
    [Range(0, 1)] public float lightningWeakness = 0f;
    [Range(0, 1)] public float spaceWeakness = 0f;
    [Range(0, 1)] public float timeWeakness = 0f;
    public float GetDamageTypeWeakness(GameEnums.DamageType type)
    {
        return type switch
        {
            GameEnums.DamageType.Physical => physicalWeakness,
            GameEnums.DamageType.Magical => magicalWeakness,
            GameEnums.DamageType.Holy => holyWeakness,
            GameEnums.DamageType.Demonic => demonicWeakness,
            _ => 0f
        };
    }

    public float GetElementWeakness(GameEnums.ElementType element)
    {
        return element switch
        {
            GameEnums.ElementType.Fire => fireWeakness,
            GameEnums.ElementType.Water => waterWeakness,
            GameEnums.ElementType.Earth => earthWeakness,
            GameEnums.ElementType.Air => airWeakness,
            GameEnums.ElementType.Ice => iceWeakness,
            GameEnums.ElementType.Lightning => lightningWeakness,
            GameEnums.ElementType.Space => spaceWeakness,
            GameEnums.ElementType.Time => timeWeakness,
            _ => 0f
        };
    }

    [Header("Aggro Settings")]
    public float aggroRangeMultiplier = 2f;
    private float aggroRange;
    private bool isAggroed = false;

    [Header("Patrol Settings")]
    public bool shouldPatrol = true;
    public List<Transform> patrolPoints;
    public float patrolWaitTime = 5f;
    public float patrolRadius = 10f;
    private float patrolSpeed;
    private Vector3 currentPatrolDestination;
    private bool isWaiting = false;
    private int currentPatrolIndex = 0;

    [Header("Tick System Settings")]
    public int ticksPerSecond = 20;

    private int _damageTicks = 0;
    private int _projectileCooldownTicks = 0;
    private float _tickAccumulator = 0f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator; 

    [Header("Death Effects")]
    public DeathEffect[] deathEffects;

    [Header("Trail Effect Settings")]
public GameObject[] trailPrefabs;
public float trailSpawnInterval = 0.2f;
public float trailLifetime = 1f;

private float trailSpawnTimer = 0f;


    private Transform currentTarget;
    private List<Transform> nearbySummons = new List<Transform>();
    private const string SUMMON_TAG = "FriendlySummon";

    private Transform player;
    private bool playerInAttackReach = false;
    private Dictionary<PlayerStats, int> damageDealers = new Dictionary<PlayerStats, int>();
    private bool isChangingPosition = false;
    protected override void Start()
    {
        base.Start();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator != null)
        {
            animator.speed = ticksPerSecond / (float)Ticker.TicksPerSecond;
        }

        patrolSpeed = speed * 0.5f;
        aggroRange = detectionRange * aggroRangeMultiplier;

        if (shouldPatrol)
        {
            if (patrolPoints.Count > 0)
            {
                currentPatrolDestination = patrolPoints[currentPatrolIndex].position;
                StartCoroutine(Patrol());
            }
            else
            {
                ChooseRandomPatrolDestination();
                StartCoroutine(RandomPatrol());
            }
        }
        Ticker.OnTickAction += HandleTick;
    }
    private void OnDestroy()
    {
        Ticker.OnTickAction -= HandleTick;
    
        if (bossType != BossType.None)
        {
            Collider2D[] minions = Physics2D.OverlapCircleAll(transform.position, minionDetectionRange);

            foreach (var minion in minions)
            {
                minion.GetComponent<EnemyAI>()?.HandleMinionBehavior();
            }
        }
    }
    private void HandleTick()
    {
        _tickAccumulator += ticksPerSecond / (float)Ticker.TicksPerSecond;

        while (_tickAccumulator >= 1f)
        {
            _tickAccumulator -= 1f;
            TickLogic();
        }
    }
    private void TickLogic()
    {
        if (playerInAttackReach && classType == EnemyClass.Melee)
        {
            _damageTicks++;
            if (_damageTicks >= Mathf.RoundToInt(damageInterval * ticksPerSecond))
            {
                DealDamageToTarget();
                _damageTicks = 0;
            }
        }
        if (classType == EnemyClass.Ranged && playerInAttackReach)
        {
            _projectileCooldownTicks++;
            if (_projectileCooldownTicks >= Mathf.RoundToInt(projectileCooldown * Ticker.TicksPerSecond))

            {
                FireProjectile();
                _projectileCooldownTicks = 0;
            }
        }
    }
    private void HandleBossBehavior()
    {
        if (bossType == BossType.Commander)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer < commanderRetreatDistance)
            {
                if (!shouldRetreat)
                {
                    commanderRetreatTimer = 0f;
                    shouldRetreat = true;
                }
                commanderRetreatTimer += Time.deltaTime;

                if (commanderRetreatTimer >= 10f || shouldRetreat)
                {
                    Vector2 retreatDirection = (transform.position - player.position).normalized;
                    float effectiveTickRatio = Ticker.TicksPerSecond == 0 ? 0f : (float)Ticker.TicksPerSecond / baselineTickRate;
float effectiveSpeed = speed * effectiveTickRatio;
                    transform.position = Vector2.MoveTowards(transform.position,
                        (Vector2)transform.position + retreatDirection * commanderRetreatDistance,
                        effectiveSpeed * Time.deltaTime);

                    if (distanceToPlayer >= commanderRetreatDistance)
                    {
                        shouldRetreat = false;
                    }
                }
            }
        }
    }
    private void HandleMinionBehavior()
    {
        if (Time.time - lastProximityCheckTime >= proximityCheckCooldown)
        {
            lastProximityCheckTime = Time.time;
            Collider2D[] nearbyBosses = Physics2D.OverlapCircleAll(transform.position, minionDetectionRange);

            foreach (var boss in nearbyBosses)
            {
                EnemyAI bossAI = boss.GetComponent<EnemyAI>();
                if (bossAI != null && bossAI.bossType != BossType.None && bossAI.faction == faction)
                {
                    float distanceToBoss = Vector2.Distance(transform.position, boss.transform.position);

                    if (distanceToBoss > minionFollowRadius)
                    {
                        float effectiveTickRatio = Ticker.TicksPerSecond == 0 ? 0f : (float)Ticker.TicksPerSecond / baselineTickRate;
float effectiveSpeed = speed * effectiveTickRatio;
                        transform.position = Vector2.MoveTowards(transform.position,
                            boss.transform.position,
                            effectiveSpeed * Time.deltaTime);
                    }
                    else if (bossAI.bossType == BossType.Stronghold &&
                             distanceToBoss < minionMinDistance)
                    {
                        Vector2 dir = (transform.position - boss.transform.position).normalized;
                        float effectiveTickRatio = Ticker.TicksPerSecond == 0 ? 0f : (float)Ticker.TicksPerSecond / baselineTickRate;
float effectiveSpeed = speed * effectiveTickRatio;
                        transform.position = Vector2.MoveTowards(transform.position,
                            (Vector2)transform.position + dir * minionMaxDistance,
                            effectiveSpeed * Time.deltaTime);
                    }
                }
            }
        }
    }

    private void Update()
{
    if (isDead || player == null) return;

    if (Ticker.TicksPerSecond == 0)
    {
        return;
    }

    FindNearbySummons();
    currentTarget = ChoosePriorityTarget();

    if (bossType != BossType.None)
    {
        HandleBossBehavior();
        return;
    }
    else if (faction != Faction.Passive)
    {
        HandleMinionBehavior();
    }
    if (currentTarget != null)
    {
        float distanceToTarget = Vector2.Distance(transform.position, currentTarget.position);
        if (distanceToTarget <= aggroRange)
        {
            if (!isAggroed) isAggroed = true;

            switch (classType)
            {
                case EnemyClass.Melee:
                    ChaseTarget();
                    break;
                case EnemyClass.Ranged:
                    if (!playerInAttackReach) ChaseTarget();
                    break;
            }
        }
    }
    else if (isAggroed)
    {
        isAggroed = false;
        HandlePatrolBehavior();
    }
    else if (shouldPatrol)
    {
        HandlePatrolBehavior();
    }
    HandleTrailSpawn();
    private void HandleTrailSpawn()
{
    if (trailPrefabs == null || trailPrefabs.Length == 0) return;

    trailSpawnTimer += Time.deltaTime;
    if (trailSpawnTimer >= trailSpawnInterval)
    {
        trailSpawnTimer = 0f;
        GameObject prefab = trailPrefabs[Random.Range(0, trailPrefabs.Length)];
        if (prefab != null)
        {
            GameObject trail = Instantiate(prefab, transform.position, Quaternion.identity);
            Destroy(trail, trailLifetime);
        }
    }
}

}
private void ChaseTarget()
{
    if (Ticker.TicksPerSecond == 0) return; // Stop movement if time stopped
    if (currentTarget == null || (classType == EnemyClass.Ranged && isChangingPosition)) return;
    float effectiveTickRatio = (float)Ticker.TicksPerSecond / baselineTickRate;
    float effectiveSpeed = speed * effectiveTickRatio;
    transform.position = Vector2.MoveTowards(
        transform.position,
        currentTarget.position,
        effectiveSpeed * Time.deltaTime
    );
}
    private void FireProjectile()
    {
        if (projectilePrefab == null || currentTarget == null) return;
        Vector2 spawnDirection = (currentTarget.position - transform.position).normalized;
        Vector2 spawnPosition = (Vector2)transform.position + spawnDirection * projectileSpawnDistance;
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
        if (projectileScript != null)
        {
            projectileScript.damage = contactDamage;
            projectileScript.damageType = damageType;
            projectileScript.elementType = elementType;
            projectileScript.target = currentTarget; // Now targets either player or summon
            projectileScript.speed = projectileSpeed * ((float)Ticker.TicksPerSecond / baselineTickRate);
        }
    }
    private void DealDamageToTarget()
    {
        if (currentTarget == null) return;
        if (currentTarget.CompareTag("Player"))
        {
            PlayerStats playerStats = currentTarget.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                Vector2 knockbackDir = (currentTarget.position - transform.position).normalized;
                playerStats.TakeDamage(contactDamage, knockbackDir, transform, damageType, false, elementType);
            }
        }
        else if (currentTarget.CompareTag(SUMMON_TAG))
        {
            Health summonHealth = currentTarget.GetComponent<Health>();
            if (summonHealth != null)
            {
                Vector2 knockbackDir = (currentTarget.position - transform.position).normalized;
                summonHealth.TakeDamage(contactDamage, knockbackDir, transform, damageType, false, elementType);
            }
        }
    }
    public override void TakeDamage(
    int damage,
    Vector2 knockbackDir,
    Transform attacker, // still here for compatibility
    GameEnums.DamageType damageType = GameEnums.DamageType.None,
    bool isCritical = false,
    GameEnums.ElementType elementType = GameEnums.ElementType.None)
{
    if (isDead) return;

    // Handle Commander retreat behavior
    if (bossType == BossType.Commander)
    {
        shouldRetreat = true;
        commanderRetreatTimer = 10f; // Force immediate retreat
    }

    // Calculate damage modifiers based on resistances
    float damageModifier = 1f - GetDamageTypeResistance(damageType);

    if (elementType != GameEnums.ElementType.None)
        damageModifier *= (1f - GetElementResistance(elementType));

    int finalDamage = Mathf.RoundToInt(damage * damageModifier);

    // Flash red effect
    StartCoroutine(FlashRed());

    // Call base class TakeDamage (attacker parameter ignored)
    base.TakeDamage(finalDamage, knockbackDir, attacker, damageType, isCritical, elementType);

    // Ranged enemies change position after taking damage
    if (classType == EnemyClass.Ranged && !isChangingPosition)
        StartCoroutine(ChangePosition());

    
}


    public float GetDamageTypeResistance(GameEnums.DamageType type)
    {
        return type switch
        {
            GameEnums.DamageType.Physical => physicalResistance,
            GameEnums.DamageType.Magical => magicalResistance,
            GameEnums.DamageType.Holy => holyResistance,
            GameEnums.DamageType.Demonic => demonicResistance,
            _ => 0f
        };
    }
    public float GetElementResistance(GameEnums.ElementType element)
    {
        return element switch
        {
            GameEnums.ElementType.Fire => fireResistance,
            GameEnums.ElementType.Water => waterResistance,
            GameEnums.ElementType.Earth => earthResistance,
            GameEnums.ElementType.Air => airResistance,
            GameEnums.ElementType.Ice => iceResistance,
            GameEnums.ElementType.Lightning => lightningResistance,
            GameEnums.ElementType.Space => spaceResistance,
            GameEnums.ElementType.Time => timeResistance,
            _ => 0f
        };
    }
    private IEnumerator ChangePosition()
{
    isChangingPosition = true;
    Vector2 directionAwayFromPlayer = (transform.position - player.position).normalized;
    Vector2 newPosition = (Vector2)transform.position + directionAwayFromPlayer * attackReachCollider.radius;
    while (Vector2.Distance(transform.position, newPosition) > 0.1f)
    {
        while (Ticker.TicksPerSecond == 0)
        {
            yield return null; 
        }
        float effectiveTickRatio = (float)Ticker.TicksPerSecond / baselineTickRate;
        float effectiveSpeed = speed * effectiveTickRatio;
        transform.position = Vector2.MoveTowards(transform.position, newPosition, effectiveSpeed * Time.deltaTime);
        yield return null;
    }
    isChangingPosition = false;
}
    private IEnumerator FlashRed()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = Color.white;
    }
    private void FindNearbySummons()
    {
        nearbySummons.Clear();
        Collider2D[] summonColliders = Physics2D.OverlapCircleAll(transform.position, detectionRange);
        foreach (Collider2D col in summonColliders)
        {
            if (col.CompareTag(SUMMON_TAG))
            {
                nearbySummons.Add(col.transform);
            }
        }
    }
    private Transform ChoosePriorityTarget()
    {
        List<Transform> potentialTargets = new List<Transform>();
        if (Vector2.Distance(transform.position, player.position) <= detectionRange)
        {
            potentialTargets.Add(player);
        }
        potentialTargets.AddRange(nearbySummons.FindAll(
            summon => Vector2.Distance(transform.position, summon.position) <= detectionRange
        ));
        Transform closestTarget = null;
        float minDistance = Mathf.Infinity;
        foreach (Transform target in potentialTargets)
        {
            float dist = Vector2.Distance(transform.position, target.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                closestTarget = target;
            }
        }
        return closestTarget;
    }
    public override void Die()
    {
        if (isDead) return;
        isDead = true;
        GiveExpToPlayer();
        SpawnDeathEffects();
        Destroy(gameObject);
    }
    private void GiveExpToPlayer()
    {
        PlayerStats topDamager = null;
        int highestDamage = 0;
        foreach (var entry in damageDealers)
        {
            if (entry.Value > highestDamage)
            {
                highestDamage = entry.Value;
                topDamager = entry.Key;
            }
        }
        topDamager?.GainExp(expReward);
    }
    private void SpawnDeathEffects()
    {
        foreach (DeathEffect effect in deathEffects)
        {
            if (Random.value > effect.chance) continue;
            for (int i = 0; i < effect.quantity; i++)
            {
                Vector2 spawnOffset = new Vector2(
                    Random.Range(-effect.spawnAreaSize.x * 0.5f, effect.spawnAreaSize.x * 0.5f),
                    Random.Range(-effect.spawnAreaSize.y * 0.5f, effect.spawnAreaSize.y * 0.5f)
                );
                Instantiate(effect.effectPrefab, (Vector2)transform.position + spawnOffset, Quaternion.identity);
            }
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (currentTarget != null && other.transform == currentTarget)
        {
            playerInAttackReach = true;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (currentTarget != null && other.transform == currentTarget)
        {
            playerInAttackReach = false;
            _damageTicks = 0;
        }
    }
    private void HandlePatrolBehavior()
    {
        if (patrolPoints.Count > 0) PatrolBehavior();
        else RandomPatrolBehavior();
    }
    private void PatrolBehavior()
    {
        if (isWaiting || patrolPoints.Count == 0) return;
        float effectivePatrolSpeed = patrolSpeed * (ticksPerSecond / (float)Ticker.TicksPerSecond);
        transform.position = Vector2.MoveTowards(transform.position, currentPatrolDestination, effectivePatrolSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, currentPatrolDestination) < 0.1f)
        {
            StartCoroutine(WaitAtPatrolPoint());
        }
    }
    private IEnumerator WaitAtPatrolPoint()
    {
        isWaiting = true;
        yield return new WaitForSeconds(patrolWaitTime);
        isWaiting = false;
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        currentPatrolDestination = patrolPoints[currentPatrolIndex].position;
    }
    private IEnumerator Patrol()
{
    while (shouldPatrol && patrolPoints.Count > 0)
    {
        while (Vector2.Distance(transform.position, currentPatrolDestination) > 0.1f)
        {
            while (Ticker.TicksPerSecond == 0)
            {
                yield return null; // pause while time stopped
            }
            float effectivePatrolSpeed = patrolSpeed * ((float)ticksPerSecond / baselineTickRate);
            transform.position = Vector2.MoveTowards(transform.position, currentPatrolDestination, effectivePatrolSpeed * Time.deltaTime);
            yield return null;
        }
        yield return new WaitForSeconds(patrolWaitTime);
        currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
        currentPatrolDestination = patrolPoints[currentPatrolIndex].position;
    }
}
    private void ChooseRandomPatrolDestination()
    {
        currentPatrolDestination = transform.position + new Vector3(
            Random.Range(-patrolRadius, patrolRadius),
            Random.Range(-patrolRadius, patrolRadius),
            0
        );
    }
    private void RandomPatrolBehavior()
    {
        if (isWaiting) return;
        float effectivePatrolSpeed = patrolSpeed * (ticksPerSecond / (float)Ticker.TicksPerSecond);
        transform.position = Vector2.MoveTowards(transform.position, currentPatrolDestination, effectivePatrolSpeed * Time.deltaTime);
        if (Vector2.Distance(transform.position, currentPatrolDestination) < 0.1f)
        {
            StartCoroutine(WaitAtRandomPatrolDestination());
        }
    }
    private IEnumerator WaitAtRandomPatrolDestination()
    {
        isWaiting = true;
        yield return new WaitForSeconds(patrolWaitTime);
        isWaiting = false;
        ChooseRandomPatrolDestination();
    }
    private IEnumerator RandomPatrol()
    {
        while (shouldPatrol)
        {
            while (Vector2.Distance(transform.position, currentPatrolDestination) > 0.1f)
{
    while (Ticker.TicksPerSecond == 0)
    {
        yield return null; 
    }
    float effectivePatrolSpeed = patrolSpeed * (ticksPerSecond / (float)Ticker.TicksPerSecond);
    transform.position = Vector2.MoveTowards(transform.position, currentPatrolDestination, effectivePatrolSpeed * Time.deltaTime);
    yield return null;
}
            yield return new WaitForSeconds(patrolWaitTime);
            ChooseRandomPatrolDestination();
        }
    }

}


