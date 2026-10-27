using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace ZombieGame.Editor
{
    public class ZombieSetupEditor : EditorWindow
    {
        private const string ControllerPath = "Assets/Project/Zombies Prefab/Zombie.controller";
        private const string ZombiesPrefabFolder = "Assets/Project/Zombies Prefab";
        private const string PlayerControllerPath = "Assets/Project/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller";
        private const string PlayerDeathFbxPath = "Assets/Project/Player Animation/Exo Gray@Death.fbx";

        private static readonly string[] AnimFbxPaths = new[]
        {
            "Assets/Project/Zombies Prefab/Warrok W Kurniawan@Zombie Idle.fbx",
            "Assets/Project/Zombies Prefab/Warrok W Kurniawan@Zombie Running.fbx",
            "Assets/Project/Zombies Prefab/Warrok W Kurniawan@Zombie Attack.fbx",
            "Assets/Project/Zombies Prefab/Warrok W Kurniawan@Zombie Scream.fbx",
            "Assets/Project/Zombies Prefab/Warrok W Kurniawan@Zombie Crawl.fbx"
        };
        [InitializeOnLoadMethod]
        private static void AutoRunOnceOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool("ZombieSetup_Executed_V3", false))
                {
                    EditorPrefs.SetBool("ZombieSetup_Executed_V3", true);
                    RunFullSetup();
                }
            };
        }

        [MenuItem("Tools/Zombie/Setup Zombie Controller & Prefabs", false, 0)]
        public static void RunFullSetup()
        {
            int step = 0;
            EditorUtility.DisplayProgressBar("Zombie Setup", "1/3: Configuring Animation FBX Rigs to Humanoid...", 0.2f);
            ConfigureAnimationFbxRigs();
            step++;

            EditorUtility.DisplayProgressBar("Zombie Setup", "2/3: Configuring Animator Controller & Transitions...", 0.6f);
            ConfigureAnimatorController();
            step++;

            EditorUtility.DisplayProgressBar("Zombie Setup", "3/4: Configuring Zombie Prefabs & Components...", 0.75f);
            ConfigureZombiePrefabs();
            step++;

            EditorUtility.DisplayProgressBar("Zombie Setup", "4/4: Configuring Player Health & Death Transition...", 0.95f);
            ConfigurePlayer();
            ConfigurePlayerDeathAnimator();
            step++;

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Zombie Setup Completed!",
                "1. Mixamo animations configured to Humanoid rig with looping enabled.\n" +
                "2. Zombie.controller transitions & parameters connected properly.\n" +
                "3. Zombie prefabs updated with Animator Controller, ZombieAI, ZombieHealth, ZombieRagdoll, & NavMeshAgent.\n\n" +
                "Ready to play!",
                "Awesome!"
            );
        }

        [MenuItem("Tools/Zombie/Open Zombie Setup Window", false, 1)]
        public static void ShowWindow()
        {
            var win = GetWindow<ZombieSetupEditor>("Zombie Setup");
            win.minSize = new Vector2(400, 320);
            win.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(12);
            GUILayout.Label("Zombie AI & Animator Quick Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool will automatically:\n" +
                "1. Convert FBX animations to Humanoid rig so they match the zombie models.\n" +
                "2. Create Animator parameters (IsRunning, IsAttacking, Scream, Speed, IsDead).\n" +
                "3. Wire all Animator transitions (Idle, Scream, Running, Attack) properly.\n" +
                "4. Attach ZombieAI, ZombieHealth, ZombieRagdoll & NavMeshAgent to all zombie prefabs.",
                MessageType.Info
            );

            GUILayout.Space(15);
            if (GUILayout.Button("1. Configure Animation FBX Rigs (Humanoid & Looping)", GUILayout.Height(32)))
            {
                ConfigureAnimationFbxRigs();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("2. Connect Zombie Animator Controller Transitions", GUILayout.Height(32)))
            {
                ConfigureAnimatorController();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("3. Setup Zombie Prefabs (AI, Health, Ragdoll)", GUILayout.Height(32)))
            {
                ConfigureZombiePrefabs();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("4. Configure Player Death in Animator & Attach Health", GUILayout.Height(32)))
            {
                ConfigurePlayerDeathAnimator();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("5. Fix & Configure Zombie Audio & AudioSources", GUILayout.Height(32)))
            {
                ConfigureAllZombieAudio();
            }

            GUILayout.Space(16);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("RUN ALL IN ONE CLICK", GUILayout.Height(44)))
            {
                RunFullSetup();
            }
            GUI.backgroundColor = Color.white;
        }

        [MenuItem("Tools/Zombie/Configure Player Death in Animator", false, 2)]
        public static void ConfigurePlayerDeathAnimator()
        {
            // 1. Ensure Death FBX is Humanoid
            if (File.Exists(PlayerDeathFbxPath))
            {
                var importer = AssetImporter.GetAtPath(PlayerDeathFbxPath) as ModelImporter;
                if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.SaveAndReimport();
                    Debug.Log("[ZombieSetup] Converted Exo Gray@Death.fbx to Humanoid.");
                }
            }

            // 2. Load Player Controller
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null)
            {
                Debug.LogWarning($"[ZombieSetup] Could not find controller at {PlayerControllerPath}");
                return;
            }

            // Ensure "Death" trigger parameter
            EnsureParameter(controller, "Death", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // Check if Death state already exists
            AnimatorState deathState = FindState(sm, "Death");
            if (deathState == null)
            {
                deathState = sm.AddState("Death", new Vector3(300, -100, 0));

                // Load Motion Clip from Exo Gray@Death.fbx
                var objects = AssetDatabase.LoadAllAssetsAtPath(PlayerDeathFbxPath);
                foreach (var obj in objects)
                {
                    if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    {
                        deathState.motion = clip;
                        break;
                    }
                }
            }

            // Check if AnyState transition to Death already exists
            bool hasAnyStateTrans = false;
            foreach (var trans in sm.anyStateTransitions)
            {
                if (trans.destinationState != null && (trans.destinationState == deathState || trans.destinationState.name.ToLower().Contains("death") || trans.destinationState.name.ToLower().Contains("die")))
                {
                    hasAnyStateTrans = true;
                    if (trans.conditions.Length == 0)
                    {
                        trans.hasExitTime = false;
                        trans.duration = 0.15f;
                        trans.canTransitionToSelf = false;
                        trans.AddCondition(AnimatorConditionMode.If, 0, "Death");
                        Debug.Log("[ZombieSetup] Fixed missing condition on user's AnyState -> Death transition!");
                    }
                    break;
                }
            }

            if (!hasAnyStateTrans)
            {
                var anyToDeath = sm.AddAnyStateTransition(deathState);
                anyToDeath.hasExitTime = false;
                anyToDeath.duration = 0.15f;
                anyToDeath.canTransitionToSelf = false;
                anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
                Debug.Log("[ZombieSetup] Added AnyState -> Death transition in Player Controller.");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            // Also ensure Player has PlayerHealth
            ConfigurePlayer();

            EditorUtility.DisplayDialog(
                "Player Death Configured!",
                "1. 'Death' trigger parameter added to Player Animator Controller.\n" +
                "2. 'Death' state wired from AnyState with Exo Gray@Death animation.\n" +
                "3. PlayerHealth attached to Player object to trigger Death on 0 HP.",
                "Awesome!"
            );
        }

        public static void ConfigureAnimationFbxRigs()
        {
            foreach (string path in AnimFbxPaths)
            {
                if (!File.Exists(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) continue;

                bool modified = false;

                // Ensure Rig is Humanoid so it matches the zombie character models
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    modified = true;
                }

                // Check default clip settings for looping
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    bool isLoopingClip = path.Contains("Idle") || path.Contains("Running") || path.Contains("Crawl");
                    for (int i = 0; i < clips.Length; i++)
                    {
                        if (clips[i].loopTime != isLoopingClip)
                        {
                            clips[i].loopTime = isLoopingClip;
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        importer.clipAnimations = clips;
                    }
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[ZombieSetup] Configured Humanoid & Looping for: {Path.GetFileName(path)}");
                }
            }
        }

        public static void ConfigureAnimatorController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[ZombieSetup] Could not find controller at {ControllerPath}");
                return;
            }

            // 1. Add Parameters
            EnsureParameter(controller, "IsRunning", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Scream", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 2. Find States
            AnimatorState idleState = FindState(sm, "Zombie Idle");
            AnimatorState screamState = FindState(sm, "Zombie Scream");
            AnimatorState runningState = FindState(sm, "Zombie Running");
            AnimatorState attackState = FindState(sm, "Zombie Attack");

            if (idleState == null || screamState == null || runningState == null || attackState == null)
            {
                Debug.LogError("[ZombieSetup] One or more required states not found in Zombie.controller!");
                return;
            }

            // Clean up existing transitions to recreate clean, deterministic transitions
            ClearTransitions(idleState);
            ClearTransitions(screamState);
            ClearTransitions(runningState);
            ClearTransitions(attackState);

            // ============================================
            // 3. Connect Transitions
            // ============================================

            // --- IDLE Transitions ---
            // Idle -> Scream (Triggered when player detected)
            var idleToScream = idleState.AddTransition(screamState);
            idleToScream.hasExitTime = false;
            idleToScream.duration = 0.15f;
            idleToScream.AddCondition(AnimatorConditionMode.If, 0, "Scream");

            // Idle -> Running
            var idleToRun = idleState.AddTransition(runningState);
            idleToRun.hasExitTime = false;
            idleToRun.duration = 0.2f;
            idleToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

            // Idle -> Attack
            var idleToAttack = idleState.AddTransition(attackState);
            idleToAttack.hasExitTime = false;
            idleToAttack.duration = 0.15f;
            idleToAttack.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

            // --- SCREAM Transitions ---
            // Scream -> Running (After scream finishes, immediately charge player)
            var screamToRun = screamState.AddTransition(runningState);
            screamToRun.hasExitTime = true;
            screamToRun.exitTime = 0.85f;
            screamToRun.duration = 0.2f;

            // Scream -> Idle (If player was lost during scream)
            var screamToIdle = screamState.AddTransition(idleState);
            screamToIdle.hasExitTime = true;
            screamToIdle.exitTime = 0.85f;
            screamToIdle.duration = 0.25f;
            screamToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // --- RUNNING Transitions ---
            // Running -> Attack (When within attack range)
            var runToAttack = runningState.AddTransition(attackState);
            runToAttack.hasExitTime = false;
            runToAttack.duration = 0.15f;
            runToAttack.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");

            // Running -> Idle (When target is lost or stopped)
            var runToIdle = runningState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = 0.25f;
            runToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            // --- ATTACK Transitions ---
            // Attack -> Running (When player moves out of attack range, finish swing then run)
            var attackToRun = attackState.AddTransition(runningState);
            attackToRun.hasExitTime = true;
            attackToRun.exitTime = 0.8f;
            attackToRun.duration = 0.2f;
            attackToRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");

            // Attack -> Idle (When player escapes or dies)
            var attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = 0.8f;
            attackToIdle.duration = 0.25f;
            attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            attackToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");

            EditorUtility.SetDirty(controller);
            Debug.Log("[ZombieSetup] Successfully wired all Animator transitions in Zombie.controller!");
        }

        public static void ConfigureZombiePrefabs()
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { ZombiesPrefabFolder });

            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                // Load prefab contents for editing
                GameObject instance = PrefabUtility.LoadPrefabContents(assetPath);
                bool modified = false;

                // 1. Animator
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                    modified = true;
                }
                if (animator.runtimeAnimatorController != controller)
                {
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    modified = true;
                }

                // 2. NavMeshAgent
                var agent = instance.GetComponent<NavMeshAgent>();
                if (agent == null)
                {
                    agent = instance.AddComponent<NavMeshAgent>();
                    agent.speed = 4f;
                    agent.stoppingDistance = 1.5f;
                    agent.radius = 0.4f;
                    agent.height = 1.8f;
                    modified = true;
                }

                // 3. Main Collider on Root
                var col = instance.GetComponent<CapsuleCollider>();
                if (col == null)
                {
                    col = instance.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0, 0.9f, 0);
                    col.radius = 0.35f;
                    col.height = 1.8f;
                    modified = true;
                }

                // 4. ZombieHealth
                var health = instance.GetComponent<ZombieHealth>();
                if (health == null)
                {
                    health = instance.AddComponent<ZombieHealth>();
                    modified = true;
                }

                // 5. ZombieRagdoll
                var ragdoll = instance.GetComponent<ZombieRagdoll>();
                if (ragdoll == null)
                {
                    ragdoll = instance.AddComponent<ZombieRagdoll>();
                    modified = true;
                }

                // 6. AudioSource (100% 3D spatial sound, no 2D bleed across the map!)
                var audioSrc = instance.GetComponent<AudioSource>();
                if (audioSrc == null)
                {
                    audioSrc = instance.AddComponent<AudioSource>();
                    modified = true;
                }

                if (audioSrc.spatialBlend != 1.0f || audioSrc.maxDistance != 20f || audioSrc.minDistance != 2f)
                {
                    audioSrc.playOnAwake = false;
                    audioSrc.spatialBlend = 1.0f; // 100% 3D sound so distance attenuation works completely
                    audioSrc.minDistance = 2.0f;
                    audioSrc.maxDistance = 20.0f;
                    audioSrc.rolloffMode = AudioRolloffMode.Logarithmic;
                    audioSrc.volume = 1f;
                    modified = true;
                }

                // 7. ZombieAI
                var ai = instance.GetComponent<ZombieAI>();
                if (ai == null)
                {
                    ai = instance.AddComponent<ZombieAI>();
                    modified = true;
                }

                // Load all audio clips from Zombie Massacre SFX Pack
                var attackGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Project/Zombies Prefab/Zombie Massacre SFX Pack/24bit/Vocal/Attack" });
                var painGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Project/Zombies Prefab/Zombie Massacre SFX Pack/24bit/Vocal/Impact Pain Hurt" });

                var intimidateClips = new System.Collections.Generic.List<AudioClip>();
                var mediumAttackClips = new System.Collections.Generic.List<AudioClip>();
                var shortGroanClips = new System.Collections.Generic.List<AudioClip>();
                var painClips = new System.Collections.Generic.List<AudioClip>();

                foreach (var g in attackGuids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    if (clip == null) continue;

                    string lower = p.ToLower();
                    if (lower.Contains("intimidate")) intimidateClips.Add(clip);
                    else if (lower.Contains("medium")) mediumAttackClips.Add(clip);
                    else if (lower.Contains("short")) shortGroanClips.Add(clip);
                }

                foreach (var g in painGuids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    if (clip != null) painClips.Add(clip);
                }

                SerializedObject soAi = new SerializedObject(ai);
                var aiAudioProp = soAi.FindProperty("_audioSource");
                if (aiAudioProp != null) aiAudioProp.objectReferenceValue = audioSrc;

                // Assign sound arrays and distinct primary clips per prefab
                int prefabIndex = System.Array.IndexOf(prefabGuids, guid);
                if (prefabIndex < 0) prefabIndex = 0;

                var screamProp = soAi.FindProperty("_screamSound");
                if (screamProp != null && intimidateClips.Count > 0)
                {
                    screamProp.objectReferenceValue = intimidateClips[prefabIndex % intimidateClips.Count];
                }

                var screamsArrayProp = soAi.FindProperty("_screamSounds");
                if (screamsArrayProp != null && intimidateClips.Count > 0)
                {
                    screamsArrayProp.arraySize = intimidateClips.Count;
                    for (int i = 0; i < intimidateClips.Count; i++)
                    {
                        screamsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = intimidateClips[i];
                    }
                }

                var attackProp = soAi.FindProperty("_attackSound");
                if (attackProp != null && mediumAttackClips.Count > 0)
                {
                    attackProp.objectReferenceValue = mediumAttackClips[prefabIndex % mediumAttackClips.Count];
                }

                var attacksArrayProp = soAi.FindProperty("_attackSounds");
                if (attacksArrayProp != null && mediumAttackClips.Count > 0)
                {
                    attacksArrayProp.arraySize = mediumAttackClips.Count;
                    for (int i = 0; i < mediumAttackClips.Count; i++)
                    {
                        attacksArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = mediumAttackClips[i];
                    }
                }

                var groanProp = soAi.FindProperty("_groanSound");
                if (groanProp != null && shortGroanClips.Count > 0)
                {
                    groanProp.objectReferenceValue = shortGroanClips[prefabIndex % shortGroanClips.Count];
                }

                var groansArrayProp = soAi.FindProperty("_groanSounds");
                if (groansArrayProp != null && shortGroanClips.Count > 0)
                {
                    groansArrayProp.arraySize = shortGroanClips.Count;
                    for (int i = 0; i < shortGroanClips.Count; i++)
                    {
                        groansArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = shortGroanClips[i];
                    }
                }

                var hearingProp = soAi.FindProperty("_soundHearingRange");
                if (hearingProp != null) hearingProp.floatValue = 20f;

                soAi.ApplyModifiedPropertiesWithoutUndo();
                modified = true;

                // Link AudioSource and pain sounds on ZombieHealth
                SerializedObject soHealth = new SerializedObject(health);
                var healthAudioProp = soHealth.FindProperty("_audioSource");
                if (healthAudioProp != null) healthAudioProp.objectReferenceValue = audioSrc;

                var hitProp = soHealth.FindProperty("_hitSound");
                if (hitProp != null && painClips.Count > 0)
                {
                    hitProp.objectReferenceValue = painClips[prefabIndex % painClips.Count];
                }

                var hitsArrayProp = soHealth.FindProperty("_hitSounds");
                if (hitsArrayProp != null && painClips.Count > 0)
                {
                    hitsArrayProp.arraySize = painClips.Count;
                    for (int i = 0; i < painClips.Count; i++)
                    {
                        hitsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = painClips[i];
                    }
                }

                var deathProp = soHealth.FindProperty("_deathSound");
                if (deathProp != null && painClips.Count > 0)
                {
                    deathProp.objectReferenceValue = painClips[(prefabIndex + 3) % painClips.Count];
                }

                var deathsArrayProp = soHealth.FindProperty("_deathSounds");
                if (deathsArrayProp != null && painClips.Count > 0)
                {
                    deathsArrayProp.arraySize = painClips.Count;
                    for (int i = 0; i < painClips.Count; i++)
                    {
                        deathsArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = painClips[i];
                    }
                }

                soHealth.ApplyModifiedPropertiesWithoutUndo();

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(instance, assetPath);
                    Debug.Log($"[ZombieSetup] Configured components, 3D audio & randomized vocal arrays on: {Path.GetFileName(assetPath)}");
                }

                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        [MenuItem("Tools/Zombie/Fix & Configure Zombie Audio", false, 2)]
        public static void ConfigureAllZombieAudio()
        {
            // 1. Ensure scene camera has AudioListener
            AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    EditorUtility.SetDirty(cam.gameObject);
                    Debug.Log("[ZombieSetup] Added missing AudioListener to Camera: " + cam.name);
                }
            }

            // 2. Configure all zombie prefabs
            ConfigureZombiePrefabs();

            // 3. Configure all scene zombie instances
            ZombieAI[] sceneZombies = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int sceneCount = 0;
            var screamClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("212cf75171688814d908356e204955b8"));
            var attackClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("be0722a3388bd4f4681aaed154aa0d2c"));
            var groanClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("fd7b66891fa24a447bb4168326b48564"));

            foreach (var z in sceneZombies)
            {
                var audioSrc = z.GetComponent<AudioSource>();
                if (audioSrc == null)
                {
                    audioSrc = z.gameObject.AddComponent<AudioSource>();
                    audioSrc.playOnAwake = false;
                    audioSrc.spatialBlend = 0.85f;
                    audioSrc.minDistance = 2.5f;
                    audioSrc.maxDistance = 45f;
                    audioSrc.rolloffMode = AudioRolloffMode.Logarithmic;
                    audioSrc.volume = 1f;
                }

                SerializedObject so = new SerializedObject(z);
                var p = so.FindProperty("_audioSource");
                if (p != null) p.objectReferenceValue = audioSrc;

                var screamP = so.FindProperty("_screamSound");
                if (screamP != null && screamClip != null) screamP.objectReferenceValue = screamClip;

                var attackP = so.FindProperty("_attackSound");
                if (attackP != null && attackClip != null) attackP.objectReferenceValue = attackClip;

                var groanP = so.FindProperty("_groanSound");
                if (groanP != null && groanClip != null) groanP.objectReferenceValue = groanClip;

                so.ApplyModifiedProperties();

                var health = z.GetComponent<ZombieHealth>();
                if (health != null)
                {
                    SerializedObject soH = new SerializedObject(health);
                    var pH = soH.FindProperty("_audioSource");
                    if (pH != null)
                    {
                        pH.objectReferenceValue = audioSrc;
                        soH.ApplyModifiedProperties();
                    }
                }

                EditorUtility.SetDirty(z.gameObject);
                sceneCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ZombieSetup] Successfully configured audio on {sceneCount} scene zombies and all prefabs!");
            EditorUtility.DisplayDialog(
                "Zombie Audio Configured!",
                $"Successfully configured AudioSource on {sceneCount} zombies in the scene and all zombie prefabs.\n" +
                $"AudioListener verified on Main Camera.",
                "OK"
            );
        }

        public static void ConfigurePlayer()
        {
            var tpc = Object.FindAnyObjectByType<StarterAssets.ThirdPersonController>();
            if (tpc != null)
            {
                if (!tpc.gameObject.CompareTag("Player"))
                {
                    tpc.gameObject.tag = "Player";
                    EditorUtility.SetDirty(tpc.gameObject);
                }
                if (tpc.GetComponent<PlayerHealth>() == null)
                {
                    tpc.gameObject.AddComponent<PlayerHealth>();
                    EditorUtility.SetDirty(tpc.gameObject);
                }
                Debug.Log("[ZombieSetup] Configured Player with 'Player' tag and PlayerHealth component.");
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == name) return;
            }
            controller.AddParameter(name, type);
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var childState in sm.states)
            {
                if (childState.state.name == name)
                    return childState.state;
            }
            return null;
        }

        private static void ClearTransitions(AnimatorState state)
        {
            var transitions = state.transitions;
            for (int i = transitions.Length - 1; i >= 0; i--)
            {
                state.RemoveTransition(transitions[i]);
            }
        }
    }
}
