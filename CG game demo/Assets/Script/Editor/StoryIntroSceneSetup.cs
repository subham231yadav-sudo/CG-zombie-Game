using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ZombieGame.Editor
{
    public static class StoryIntroSceneSetup
    {
        [MenuItem("Tools/Setup Story Intro Scene", false, 10)]
        public static void SetupStoryIntro()
        {
            // 1. Ensure StoryIntroManager GameObject exists in current scene
            StoryIntroManager manager = Object.FindFirstObjectByType<StoryIntroManager>();
            if (manager == null)
            {
                GameObject introObj = new GameObject("StoryIntroManager");
                Undo.RegisterCreatedObjectUndo(introObj, "Create StoryIntroManager");
                manager = introObj.AddComponent<StoryIntroManager>();
            }

            // Auto assign slides and assets
            manager.AutoAssignStoryAssets();
            EditorUtility.SetDirty(manager);

            // 2. Configure Build Settings: starter.unity at index 0, SampleScene.unity at index 1
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();

            string starterPath = "Assets/Scenes/starter.unity";
            string sampleScenePath = "Assets/Scenes/SampleScene.unity";

            buildScenes.Add(new EditorBuildSettingsScene(starterPath, true));
            buildScenes.Add(new EditorBuildSettingsScene(sampleScenePath, true));

            EditorBuildSettings.scenes = buildScenes.ToArray();

            // Mark active scene dirty so user can save or it saves automatically
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[StoryIntroSceneSetup] Story Intro configured successfully! 'starter' is Scene 0, 'SampleScene' is Scene 1.");
            EditorUtility.DisplayDialog("Story Intro Setup Complete", 
                "Story Intro Manager has been configured with all 4 images and voiceovers!\n\nBuild Settings updated:\n- Scene 0: starter.unity\n- Scene 1: SampleScene.unity\n\nPress PLAY in Unity to test the full story intro!", "Awesome!");
        }
    }
}
