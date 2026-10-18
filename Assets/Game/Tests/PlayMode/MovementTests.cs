using System.Collections;
using Jrpg;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class MovementTests
{
    [UnityTest] public IEnumerator GladeHasPlayableWiring()
    {
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Glade");
        var mover = Object.FindFirstObjectByType<PlayerMover>();
        var camera = Camera.main;
        var encounter = Object.FindFirstObjectByType<DemoEncounter>();
        Assert.IsNotNull(mover); Assert.IsNotNull(camera); Assert.IsTrue(camera.orthographic);
        Assert.AreSame(mover.transform, camera.GetComponent<FollowCamera>().Target);
        Assert.IsNotNull(encounter.HeroDefinition); Assert.IsNotNull(encounter.EnemyDefinition);
        Assert.IsNotNull(encounter.AttackSkill); Assert.IsNotNull(encounter.Quest);
        Assert.IsNotNull(UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline);
        Assert.Greater(Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length, 1);
        Assert.IsTrue(mover.GetComponent<CharacterController>().enabled);
        // Leave no scene objects to interfere with the isolated collision test.
        var empty = UnityEngine.SceneManagement.SceneManager.CreateScene("TestCleanup");
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(empty);
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync("Glade");
    }

    [UnityTest] public IEnumerator ControllerStopsAtWall()
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(20, 1, 20);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(2, 1, 0); wall.transform.localScale = new Vector3(1, 3, 8);
        var obj = new GameObject("Test player"); obj.transform.position = new Vector3(0, .05f, 0);
        var controller = obj.AddComponent<CharacterController>(); controller.height = 1.4f; controller.center = new Vector3(0, .7f, 0); controller.radius = .28f;
        var mover = obj.AddComponent<PlayerMover>(); mover.enabled = false;
        try {
            yield return null; Physics.SyncTransforms();
            for (int i = 0; i < 100; i++) { mover.Move(Vector2.right, .02f); yield return null; }
            Assert.Greater(obj.transform.position.x, .5f); Assert.Less(obj.transform.position.x, 1.4f);
        } finally { Object.Destroy(obj); Object.Destroy(wall); Object.Destroy(floor); }
    }
}
