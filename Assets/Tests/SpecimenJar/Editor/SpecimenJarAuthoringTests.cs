using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.Tests
{
    // Native Editor tests: reflection keeps production in its existing assemblies.
    // These are not exercised by the pure managed policy harness or source checker.
    public sealed class SpecimenJarAuthoringTests
    {
        private const string Prefab = "Assets/RunawayChimps/Toys/ReactiveSpecimenJar/ReactiveSpecimenJar.prefab";
        private static Type Production => Type.GetType("RunawayChimps.Toys.ReactiveSpecimenJar, Assembly-CSharp", true);
        private static Type EditorType => Type.GetType("ReactiveSpecimenJarEditor, Assembly-CSharp-Editor", true);
        private Scene scene, previousScene;
        private int undoFloor;

        private static T Get<T>(Component jar, string field)
            => (T)Production.GetField(field).GetValue(jar);
        private static void Set(Component jar, string field, object value)
            => Production.GetField(field).SetValue(jar, value);
        private static void Create(Component jar)
            => EditorType.GetMethod("CreatePreviewHandles", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { jar });

        private static bool IsSceneInstance(Component item)
            => (bool)EditorType.GetMethod("IsSceneInstance", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { item });

        [SetUp]
        public void SetUp()
        {
            previousScene = SceneManager.GetActiveScene();
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            // Never clear the user's Undo history or save/replace an existing scene.
            Undo.IncrementCurrentGroup();
            undoFloor = Undo.GetCurrentGroup();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(undoFloor);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private Component Fixture()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Assert.That(asset, Is.Not.Null, "The actual serialized prefab must import.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            // Nonidentity authoring catches pose/parenting errors hidden by an origin fixture.
            instance.transform.SetPositionAndRotation(new Vector3(3f, 2f, -4f), Quaternion.Euler(0f, 37f, 0f));
            instance.transform.localScale = Vector3.one * 1.4f;
            return instance.GetComponent(Production);
        }

        private static void AssertPose(Transform item, Vector3 position, Quaternion rotation)
        {
            Assert.That(item, Is.Not.Null);
            Assert.That(Vector3.Distance(item.localPosition, position), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(item.localRotation, rotation), Is.LessThan(0.01f));
            Assert.That(item.localScale, Is.EqualTo(Vector3.one));
            Assert.That(item.CompareTag("EditorOnly"), Is.True);
        }

        private static void AssertHandles(Component jar)
        {
            var head = Get<Transform>(jar, "previewHead");
            var left = Get<Transform>(jar, "previewLeftHand");
            var right = Get<Transform>(jar, "previewRightHand");
            AssertPose(head, new Vector3(0f, 0.06f, 0.8f), Quaternion.Euler(0f, 180f, 0f));
            AssertPose(left, new Vector3(-0.24f, 0f, 0.4f), Quaternion.identity);
            AssertPose(right, new Vector3(0.4f, 0f, 0.55f), Quaternion.identity);
            Assert.That(left.parent, Is.SameAs(head.parent));
            Assert.That(right.parent, Is.SameAs(head.parent));
            AssertPose(head.parent, Vector3.zero, Quaternion.identity);
            Assert.That(head.parent.parent, Is.SameAs(jar.transform));
            Assert.That(head.gameObject.scene, Is.EqualTo(jar.gameObject.scene));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreviewUndoRedoRestoresSettingsReferencesAndAllPoses(bool previousPreview)
        {
            Component jar = Fixture();
            Set(jar, "editorPreview", previousPreview);
            int children = jar.transform.childCount;
            Create(jar);
            Assert.That(Get<bool>(jar, "editorPreview"), Is.True);
            AssertHandles(jar);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            Assert.That(Get<bool>(jar, "editorPreview"), Is.EqualTo(previousPreview));
            Assert.That(Get<Transform>(jar, "previewHead") == null, Is.True);
            Assert.That(Get<Transform>(jar, "previewLeftHand") == null, Is.True);
            Assert.That(Get<Transform>(jar, "previewRightHand") == null, Is.True);
            Assert.That(jar.transform.childCount, Is.EqualTo(children));
            Undo.PerformRedo();
            Assert.That(Get<bool>(jar, "editorPreview"), Is.True);
            Assert.That(jar.transform.childCount, Is.EqualTo(children + 1));
            AssertHandles(jar);
            Assert.That(jar.transform.position, Is.EqualTo(new Vector3(3f, 2f, -4f)));
        }

        [Test]
        public void PreviewCanBeCreatedAgainAfterUndo()
        {
            Component jar = Fixture();
            int children = jar.transform.childCount;
            Create(jar);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            Create(jar);
            Assert.That(jar.transform.childCount, Is.EqualTo(children + 1));
            AssertHandles(jar);
        }

        [Test]
        public void PartialExistingPreviewIsNotReplacedOrEnabled()
        {
            Component jar = Fixture();
            var existing = new GameObject("Existing preview hand");
            SceneManager.MoveGameObjectToScene(existing, scene);
            existing.transform.SetParent(jar.transform, false);
            existing.transform.localPosition = new Vector3(1f, 2f, 3f);
            Set(jar, "previewLeftHand", existing.transform);
            Set(jar, "editorPreview", false);
            int children = jar.transform.childCount;
            Create(jar);
            Assert.That(Get<Transform>(jar, "previewLeftHand"), Is.SameAs(existing.transform));
            Assert.That(existing.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(Get<Transform>(jar, "previewHead") == null, Is.True);
            Assert.That(Get<bool>(jar, "editorPreview"), Is.False);
            Assert.That(jar.transform.childCount, Is.EqualTo(children));
        }

        [Test]
        public void PrefabContentsPreviewSceneCannotAcquireTestHandles()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Component jar = contents.GetComponent(Production);
                Assert.That(EditorUtility.IsPersistent(jar), Is.False, "Old guard alone would permit this.");
                Assert.That(EditorSceneManager.IsPreviewScene(contents.scene), Is.True);
                Assert.That(IsSceneInstance(jar.transform), Is.False, "Even manually assigned handles must reject prefab contents.");
                string before = EditorJsonUtility.ToJson(jar);
                int children = contents.transform.childCount;
                Create(jar);
                Assert.That(EditorJsonUtility.ToJson(jar), Is.EqualTo(before));
                Assert.That(contents.transform.childCount, Is.EqualTo(children));
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        [Test]
        public void PersistentPrefabAssetCannotAcquireTestHandles()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Component jar = asset.GetComponent(Production);
            Assert.That(IsSceneInstance(asset.transform), Is.False, "An asset dragged into a handle field must not be edited.");
            Assert.That(IsSceneInstance(Fixture().transform), Is.True);
            string before = EditorJsonUtility.ToJson(jar);
            int children = asset.transform.childCount;
            Create(jar);
            Assert.That(EditorJsonUtility.ToJson(jar), Is.EqualTo(before));
            Assert.That(asset.transform.childCount, Is.EqualTo(children));
        }
    }
}
