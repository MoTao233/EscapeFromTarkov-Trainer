using System;
using System.Collections.Generic;
using EFT.Trainer.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public static class TrainerHierarchyChecks
{
    public static int Run()
    {
        int checks = 0;
        var collector = new HighlightHierarchy();
        var renderers = new List<Renderer>();
        var root = new GameObject("Container visual root");
        var body = Mesh(root.transform, "Box body", Vector3.zero);
        var lid = Mesh(root.transform, "Moving lid", Vector3.up);
        var owner = lid.gameObject.AddComponent<TrainerInteractionStub>();
        collector.Collect(owner, null, true, 6, renderers);
        Require(renderers.Contains(body) && renderers.Contains(lid) && renderers.Count == 2, "lid interaction includes the sibling box body", ref checks);

        var collider = new GameObject("Bag interaction collider");
        collider.transform.parent = root.transform;
        UnityEngine.Object.DestroyImmediate(owner);
        owner = collider.AddComponent<TrainerInteractionStub>();
        collector.Collect(owner, null, true, 6, renderers);
        Require(renderers.Contains(body) && renderers.Contains(lid), "invisible interaction node finds travel/medical bag geometry on siblings", ref checks);

        collector.Collect(owner, new[] { root, root, body.gameObject }, true, 6, renderers);
        Require(renderers.Count == 2, "explicit container visual roots are deduplicated with parent discovery", ref checks);

        var otherRoot = new GameObject("Another container");
        otherRoot.transform.parent = root.transform;
        var otherBody = Mesh(otherRoot.transform, "Unrelated container body", Vector3.right * 2);
        otherBody.gameObject.AddComponent<TrainerInteractionStub>();
        collector.Collect(owner, null, true, 6, renderers);
        Require(renderers.Count == 0, "shared parent containing another interactable is rejected", ref checks);
        collector.Collect(owner, new[] { body.gameObject }, true, 6, renderers);
        Require(renderers.Count == 1 && renderers[0] == body, "explicit body reference works even under a shared scene parent", ref checks);
        UnityEngine.Object.DestroyImmediate(otherRoot);

        var wall = Mesh(root.transform, "Building wall", Vector3.zero);
        wall.transform.localScale = new Vector3(30, 10, 1);
        collector.Collect(owner, null, true, 6, renderers);
        Require(renderers.Count == 0, "oversized scene parent is not outlined", ref checks);
        UnityEngine.Object.DestroyImmediate(wall.gameObject);

        var shadow = Mesh(root.transform, "Shadow proxy", Vector3.zero);
        shadow.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        collector.Collect(owner, null, true, 6, renderers);
        Require(renderers.Count == 2 && !renderers.Contains(shadow), "shadow-only helper meshes are excluded", ref checks);
        UnityEngine.Object.DestroyImmediate(root);

        var doorway = new GameObject("Doorway");
        var door = Mesh(doorway.transform, "Moving door leaf", Vector3.zero);
        door.transform.localScale = new Vector3(1, 2, 0.1f);
        owner = door.gameObject.AddComponent<TrainerInteractionStub>();
        wall = Mesh(doorway.transform, "Wall beside door", Vector3.right * 2);
        collector.Collect(owner, null, false, 8, renderers);
        Require(renderers.Count == 1 && renderers[0] == door, "door outline excludes the wall and fixed parent geometry", ref checks);
        var target = new HighlightTarget();
        target.Refresh(renderers);
        var camera = new GameObject("Bounds camera").AddComponent<Camera>();
        target.TryGetBounds(camera, out var closed);
        door.transform.position = Vector3.right * 3;
        door.transform.rotation = Quaternion.Euler(0, 90, 0);
        target.TryGetBounds(camera, out var opened);
        Require(Vector3.Distance(closed.center, opened.center) > 2 && opened.size.z > opened.size.x,
            "cached door renderers follow opening translation and rotation", ref checks);
        door.gameObject.SetActive(false);
        Require(!target.TryGetBounds(camera, out _), "destroyed/disabled door visuals stop rendering", ref checks);
        door.gameObject.SetActive(true);
        var added = Mesh(door.transform, "Late-loaded handle", Vector3.up * 0.2f);
        collector.Collect(owner, null, false, 8, renderers);
        Require(renderers.Contains(added), "refresh discovers late-loaded visual parts", ref checks);
        UnityEngine.Object.DestroyImmediate(doorway);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);

        root = new GameObject("Large scene group");
        owner = root.AddComponent<TrainerInteractionStub>();
        Mesh(root.transform, "Mesh beneath an oversized scene root", Vector3.zero);
        for (int i = 0; i < 260; i++) new GameObject("Scene node").transform.parent = root.transform;
        collector.Collect(owner, null, true, 6, renderers);
        Require(renderers.Count == 0, "hierarchy scan has a bounded node budget", ref checks);
        UnityEngine.Object.DestroyImmediate(root);
        return checks;
    }

    private static MeshRenderer Mesh(Transform parent, string name, Vector3 position)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.parent = parent;
        go.transform.localPosition = position;
        return go.GetComponent<MeshRenderer>();
    }

    private static void Require(bool condition, string label, ref int checks)
    {
        if (!condition) throw new Exception("HIERARCHY CHECK FAILED: " + label);
        checks++;
        Debug.Log("PASS " + label);
    }
}
